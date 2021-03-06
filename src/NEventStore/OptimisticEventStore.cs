namespace NEventStore
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NEventStore.Logging;
    using NEventStore.Persistence;
    using ALinq;

    public class OptimisticEventStore : IStoreEvents, ICommitEvents
    {
        private static readonly ILog Logger = LogFactory.BuildLogger(typeof (OptimisticEventStore));
        private readonly IPersistStreams _persistence;
        private readonly IEnumerable<IPipelineHook> _pipelineHooks;
        private readonly ISystemTimeProvider _systemTimeProvider;

        public OptimisticEventStore(
            IPersistStreams persistence, 
            IEnumerable<IPipelineHook> pipelineHooks,
            ISystemTimeProvider systemTimeProvider
        )
        {
            if (persistence == null)
            {
                throw new ArgumentNullException("persistence");
            }

            _pipelineHooks = pipelineHooks ?? new IPipelineHook[0];
            _persistence = new PipelineHooksAwarePersistanceDecorator(persistence, _pipelineHooks);
            _systemTimeProvider = systemTimeProvider;
        }

        public virtual IAsyncEnumerable<ICommit> GetFrom(string bucketId, string streamId, int minRevision, int maxRevision)
        {
            return _persistence.GetFrom(bucketId, streamId, minRevision, maxRevision);
        }

        public virtual async Task<ICommit> Commit(CommitAttempt attempt)
        {
            Guard.NotNull(() => attempt, attempt);
            foreach (var hook in _pipelineHooks)
            {
                Logger.Debug(Resources.InvokingPreCommitHooks, attempt.CommitId, hook.GetType());
                if (await hook.PreCommit(attempt))
                {
                    continue;
                }

                Logger.Info(Resources.CommitRejectedByPipelineHook, hook.GetType(), attempt.CommitId);
                return null;
            }

            Logger.Info(Resources.CommittingAttempt, attempt.CommitId, attempt.Events.Count);
            ICommit commit = await _persistence.Commit(attempt);

            foreach (var hook in _pipelineHooks)
            {
                Logger.Debug(Resources.InvokingPostCommitPipelineHooks, attempt.CommitId, hook.GetType());
                await hook.PostCommit(commit);
            }
            return commit;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual Task<IEventStream> CreateStream(string bucketId, string streamId)
        {
            Logger.Info(Resources.CreatingStream, streamId, bucketId);
            return Task.FromResult<IEventStream>(new OptimisticEventStream(bucketId, streamId, this, _systemTimeProvider));
        }

		public virtual async Task<IEventStream> OpenStream(string bucketId, string streamId, int minRevision, int maxRevision)
        {
            maxRevision = maxRevision <= 0 ? int.MaxValue : maxRevision;

            Logger.Debug(Resources.OpeningStreamAtRevision, streamId, bucketId, minRevision, maxRevision);

			var stream = new OptimisticEventStream(bucketId, streamId, this, _systemTimeProvider);
			await stream.Initialize(minRevision, maxRevision);
            return stream;
        }

		public virtual async Task<IEventStream> OpenStream(ISnapshot snapshot, int maxRevision)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException("snapshot");
            }

            Logger.Debug(Resources.OpeningStreamWithSnapshot, snapshot.StreamId, snapshot.StreamRevision, maxRevision);
            maxRevision = maxRevision <= 0 ? int.MaxValue : maxRevision;

			var stream = new OptimisticEventStream(
				snapshot.BucketId, snapshot.StreamId, this, _systemTimeProvider);
			await stream.Initialize(snapshot, maxRevision);
			return stream;
        }

        public virtual IPersistStreams Advanced
        {
            get { return _persistence; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            Logger.Info(Resources.ShuttingDownStore);
            _persistence.Dispose();
            foreach (var hook in _pipelineHooks)
            {
                hook.Dispose();
            }
        }
    }
}