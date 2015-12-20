namespace NEventStore
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading.Tasks;
    using NEventStore.Logging;
    using ALinq;

    [SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix",
		Justification = "This behaves like a stream--not a .NET 'Stream' object, but a stream nonetheless.")]
	public sealed class OptimisticEventStream : IEventStream
	{
		private static readonly ILog Logger = LogFactory.BuildLogger(typeof(OptimisticEventStream));
		private readonly ICollection<EventMessage> _committed = new LinkedList<EventMessage>();
		private readonly IDictionary<string, object> _committedHeaders = new Dictionary<string, object>();
		private readonly ICollection<EventMessage> _events = new LinkedList<EventMessage>();
		private readonly ICollection<Guid> _identifiers = new HashSet<Guid>();
		private readonly ICommitEvents _persistence;
		private readonly IDictionary<string, object> _uncommittedHeaders = new Dictionary<string, object>();
        private readonly ISystemTimeProvider _systemTypeProvider;
        private bool _disposed;
        

        public OptimisticEventStream(
            string bucketId, 
            string streamId, 
            ICommitEvents persistence,
            ISystemTimeProvider systemTypeProvider
        )
		{
			BucketId = bucketId;
			StreamId = streamId;
			_persistence = persistence;
            _systemTypeProvider = systemTypeProvider;

        }

		public string BucketId
		{
			get;
			private set;
		}
		public string StreamId
		{
			get;
			private set;
		}
		public int StreamRevision
		{
			get;
			private set;
		}
		public int CommitSequence
		{
			get;
			private set;
		}

		public ICollection<EventMessage> CommittedEvents
		{
			get
			{
				return new ImmutableCollection<EventMessage>(_committed);
			}
		}

		public IDictionary<string, object> CommittedHeaders
		{
			get
			{
				return _committedHeaders;
			}
		}

		public ICollection<EventMessage> UncommittedEvents
		{
			get
			{
				return new ImmutableCollection<EventMessage>(_events);
			}
		}

		public IDictionary<string, object> UncommittedHeaders
		{
			get
			{
				return _uncommittedHeaders;
			}
		}

		public async Task Initialize(int minRevision, int maxRevision)
		{
			var commits = _persistence.GetFrom(BucketId, StreamId, minRevision, maxRevision);
			await PopulateStream(minRevision, maxRevision, commits);

			if (minRevision > 0 && _committed.Count == 0)
			{
				throw new StreamNotFoundException();
			}
		}

		public async Task Initialize(ISnapshot snapshot, int maxRevision)
		{
			if (snapshot.BucketId != BucketId || snapshot.StreamId != StreamId)
			{
				throw new ArgumentException("Invalid snapshot.");
			}

			var  commits = _persistence.GetFrom(snapshot.BucketId, snapshot.StreamId, snapshot.StreamRevision, maxRevision);
			await PopulateStream(snapshot.StreamRevision + 1, maxRevision, commits);
			StreamRevision = snapshot.StreamRevision + _committed.Count;
		}

		public void Add(EventMessage uncommittedEvent)
		{
			if (uncommittedEvent == null || uncommittedEvent.Body == null)
			{
				return;
			}

			Logger.Debug(Resources.AppendingUncommittedToStream, StreamId);
			_events.Add(uncommittedEvent);
		}

		public async Task CommitChanges(Guid commitId)
		{
			Logger.Debug(Resources.AttemptingToCommitChanges, StreamId);

			if (_identifiers.Contains(commitId))
			{
				throw new DuplicateCommitException();
			}

			if (!HasChanges())
			{
				return;
			}

			ConcurrencyException exceptionToThrow = null;
			try
			{
				await PersistChanges(commitId);
			}
			catch (ConcurrencyException exception)
			{
				exceptionToThrow = exception;
				Logger.Info(Resources.UnderlyingStreamHasChanged, StreamId);
			}

			if (exceptionToThrow != null)
			{
				var commits = _persistence.GetFrom(BucketId, StreamId, StreamRevision + 1, int.MaxValue);
				await PopulateStream(StreamRevision + 1, int.MaxValue, commits);

				throw exceptionToThrow;
			}
		}

		public void ClearChanges()
		{
			Logger.Debug(Resources.ClearingUncommittedChanges, StreamId);
			_events.Clear();
			_uncommittedHeaders.Clear();
		}

		private Task PopulateStream(int minRevision, int maxRevision, IAsyncEnumerable<ICommit> commits)
		{
            commits = commits ?? AsyncEnumerable.Empty<ICommit>();
            return commits.ForEach(context =>
            {
                var commit = context.Item;
                Logger.Verbose(Resources.AddingCommitsToStream, commit.CommitId, commit.Events.Count, StreamId);
                _identifiers.Add(commit.CommitId);

                CommitSequence = commit.CommitSequence;
                int currentRevision = commit.StreamRevision - commit.Events.Count + 1;
                if (currentRevision > maxRevision)
                {
                    context.Break();
                    return Task.FromResult(false);
                }

                CopyToCommittedHeaders(commit);
                CopyToEvents(minRevision, maxRevision, currentRevision, commit);
                return Task.FromResult(false);
            });
		}

		private void CopyToCommittedHeaders(ICommit commit)
		{
			foreach (var key in commit.Headers.Keys)
			{
				_committedHeaders[key] = commit.Headers[key];
			}
		}

		private void CopyToEvents(int minRevision, int maxRevision, int currentRevision, ICommit commit)
		{
			foreach (var @event in commit.Events)
			{
				if (currentRevision > maxRevision)
				{
					Logger.Debug(Resources.IgnoringBeyondRevision, commit.CommitId, StreamId, maxRevision);
					break;
				}

				if (currentRevision++ < minRevision)
				{
					Logger.Debug(Resources.IgnoringBeforeRevision, commit.CommitId, StreamId, maxRevision);
					continue;
				}

				_committed.Add(@event);
				StreamRevision = currentRevision - 1;
			}
		}

		private bool HasChanges()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(Resources.AlreadyDisposed);
			}

			if (_events.Count > 0)
			{
				return true;
			}

			Logger.Warn(Resources.NoChangesToCommit, StreamId);
			return false;
		}

		private async Task PersistChanges(Guid commitId)
		{
			CommitAttempt attempt = BuildCommitAttempt(commitId);

			Logger.Debug(Resources.PersistingCommit, commitId, StreamId);
			ICommit commit = await _persistence.Commit(attempt);

			if (commit != null)
			{
				await PopulateStream(StreamRevision + 1, attempt.StreamRevision, AsyncEnumerable.Create(commit));
			}

			ClearChanges();
		}

		private CommitAttempt BuildCommitAttempt(Guid commitId)
		{
			Logger.Debug(Resources.BuildingCommitAttempt, commitId, StreamId);
			return new CommitAttempt(
				BucketId,
				StreamId,
				StreamRevision + _events.Count,
				commitId,
				CommitSequence + 1,
                _systemTypeProvider.UtcNow,
				_uncommittedHeaders.ToDictionary(x => x.Key, x => x.Value),
				_events.ToList());
		}

		public void Dispose()
		{
			_disposed = true;
		}
	}
}