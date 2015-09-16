namespace NEventStore.Client
{
    using System;
    using System.Collections.Generic;
    using System.Reactive.Linq;
    using System.Reactive.Threading.Tasks;
    using System.Threading.Tasks;
    using FakeItEasy;
	using FluentAssertions;
    using NEventStore.Persistence;
    using NEventStore.Persistence.AcceptanceTests;
    using NEventStore.Persistence.AcceptanceTests.BDD;
    using Xunit;

    public class CreatingPollingClientTests
    {
        [Fact]
        public void When_persist_streams_is_null_then_should_throw()
        {
            Catch.Exception(() => new PollingClient(null)).Should().BeOfType<ArgumentNullException>();
        }

        [Fact]
        public void When_interval_less_than_zero_then_should_throw()
        {
            Catch.Exception(() => new PollingClient(A.Fake<IPersistStreams>(),-1)).Should().BeOfType<ArgumentException>();
        }

        [Fact]
        public void When_interval_is_zero_then_should_throw()
        {
            Catch.Exception(() => new PollingClient(A.Fake<IPersistStreams>(), 0)).Should().BeOfType<ArgumentException>();
        }
    }

    public abstract class using_polling_client : SpecificationBase
    {
        protected const int PollingInterval = 100;
        private PollingClient _pollingClient;
        private IStoreEvents _storeEvents;

        protected PollingClient PollingClient
        {
            get { return _pollingClient; }
        }

        protected IStoreEvents StoreEvents
        {
            get { return _storeEvents; }
        }

        protected override Task Context()
        {
            _storeEvents = Wireup.Init().UsingInMemoryPersistence().Build();
            _pollingClient = new PollingClient(_storeEvents.Advanced, PollingInterval);
			return Task.FromResult(true);
        }

        protected override void CleanupSynch()
        {
            _storeEvents.Dispose();
        }
    }

    public class when_commit_is_comitted_before_subscribing : using_polling_client
    {
        private IObserveCommits _observeCommits;
        private Task<ICommit> _commitObserved;

        protected override Task Context()
        {
            base.Context();
            StoreEvents.Advanced.CommitSingle();
            _observeCommits = PollingClient.ObserveFrom();
            _commitObserved = _observeCommits.FirstAsync().ToTask();
			return Task.FromResult(true);
        }

        protected override async Task Because()
        {
            // We intentionally do not await here!
            await _observeCommits.Start();
        }

        protected override void CleanupSynch()
        {
            _observeCommits.Dispose();
        }

        [Fact]
        public async Task should_observe_commit()
        {
            var task = await Task.WhenAny(_commitObserved, Task.Delay(PollingInterval * 2));
            Assert.Equal(task, _commitObserved);
        }
    }

    public class when_commit_is_comitted_before_and_after_subscribing : using_polling_client
    {
        private IObserveCommits _observeCommits;
        private Task<ICommit> _twoCommitsObserved;

        protected override Task Context()
        {
            base.Context();
            StoreEvents.Advanced.CommitSingle();
            _observeCommits = PollingClient.ObserveFrom();
            _twoCommitsObserved = _observeCommits.Take(2).ToTask();
			return Task.FromResult(true);
        }

        protected override async Task Because()
        {
            await _observeCommits.Start();
            await StoreEvents.Advanced.CommitSingle();
        }

        protected override void CleanupSynch()
        {
            _observeCommits.Dispose();
        }

        [Fact]
        public async Task should_observe_two_commits()
        {
            var task = await Task.WhenAny(_twoCommitsObserved, Task.Delay(PollingInterval * 2));
            Assert.Equal(task, _twoCommitsObserved);
        }
    }

    public class with_two_observers_and_multiple_commits : using_polling_client
    {
        private IObserveCommits _observeCommits1;
        private IObserveCommits _observeCommits2;
        private Task<ICommit> _observeCommits1Complete;
        private Task<ICommit> _observeCommits2Complete;

        protected override async Task Context()
        {
            await base.Context();
            await StoreEvents.Advanced.CommitSingle();
            _observeCommits1 = PollingClient.ObserveFrom();
            _observeCommits1Complete = _observeCommits1.Take(5).ToTask();

            _observeCommits2 = PollingClient.ObserveFrom();
            _observeCommits2Complete = _observeCommits1.Take(10).ToTask();
        }

        protected override async Task Because()
        {
            await _observeCommits1.Start();
            await _observeCommits2.Start();

            for (int i = 0; i < 15; i++)
            {
                await StoreEvents.Advanced.CommitSingle();
            }
            
        }

        protected override void CleanupSynch()
        {
            _observeCommits1.Dispose();
            _observeCommits2.Dispose();
        }

        [Fact]
        public async Task should_observe_commits_on_first_observer()
        {
            var task = await Task.WhenAny(_observeCommits1Complete, Task.Delay(PollingInterval * 10));
            Assert.Equal(task, _observeCommits1Complete);
        }

        [Fact]
        public async Task should_observe_commits_on_second_observer()
        {
            var task = await Task.WhenAny(_observeCommits2Complete, Task.Delay(PollingInterval * 10));
            Assert.Equal(task, _observeCommits2Complete);
        }
    }

    public class with_two_subscriptions_on_a_single_observer_and_multiple_commits : using_polling_client
    {
        private IObserveCommits _observeCommits1;
        private Task<ICommit> _observeCommits1Complete;
        private Task<ICommit> _observeCommits2Complete;

        protected override async Task Context()
        {
            await base.Context();
            await StoreEvents.Advanced.CommitSingle();
            _observeCommits1 = PollingClient.ObserveFrom();
            _observeCommits1Complete = _observeCommits1.Take(5).ToTask();
            _observeCommits2Complete = _observeCommits1.Take(10).ToTask();
        }

        protected override async Task Because()
        {
            await _observeCommits1.Start();

            for (int i = 0; i < 15; i++)
            {
                await StoreEvents.Advanced.CommitSingle();
            }
        }

        protected override void CleanupSynch()
        {
            _observeCommits1.Dispose();
        }

        [Fact]
        public async Task should_observe_commits_on_first_observer()
        {
            var task = await Task.WhenAny(_observeCommits1Complete, Task.Delay(PollingInterval * 10));
            Assert.Equal(task, _observeCommits1Complete);
        }

        [Fact]
        public async Task should_observe_commits_on_second_observer()
        {
            var task = await Task.WhenAny(_observeCommits2Complete, Task.Delay(PollingInterval * 10));
            Assert.Equal(task, _observeCommits2Complete);
        }
    }

    public class when_resuming : using_polling_client
    {
        private IObserveCommits _observeCommits;
        private Task<ICommit> _commitObserved;

        protected override async Task Context()
        {
            await base.Context();
            await StoreEvents.Advanced.CommitSingle();
            _observeCommits = PollingClient.ObserveFrom();
            _commitObserved = _observeCommits.FirstAsync().ToTask();

            await _observeCommits.Start();

            var task = await Task.WhenAny(_commitObserved, Task.Delay(PollingInterval * 2));
            task.Should().Be(_commitObserved);

            _observeCommits.Dispose();

            await StoreEvents.Advanced.CommitSingle();
            string checkpointToken = _commitObserved.Result.CheckpointToken;
            _observeCommits = PollingClient.ObserveFrom(checkpointToken);
        }

        protected override async Task Because()
        {
            // NOTE: We do not await intentionally here!
            _commitObserved = _observeCommits.FirstAsync().ToTask();

            await _observeCommits.Start();
        }

        protected override void CleanupSynch()
        {
            _observeCommits.Dispose();
        }

        [Fact]
        public async Task should_observe_commit()
        {
            var task = await Task.WhenAny(_commitObserved, Task.Delay(PollingInterval * 2));
            Assert.Equal(task, _commitObserved);
        }
    }

    public class when_polling_now : using_polling_client
    {
        private IObserveCommits _observeCommits;
        private Task<ICommit> _commitObserved;

        protected override async Task Context()
        {
            await base.Context();
			await StoreEvents.Advanced.CommitSingle();
            _observeCommits = PollingClient.ObserveFrom();
            _commitObserved = _observeCommits.FirstAsync().ToTask();
        }

        protected override async Task Because()
        {
            await _observeCommits.PollNow();
        }

        protected override void CleanupSynch()
        {
            _observeCommits.Dispose();
        }

        [Fact]
        public async Task should_observe_commit()
        {
            var task = await Task.WhenAny(_commitObserved, Task.Delay(PollingInterval * 2));
            Assert.Equal(task, _commitObserved);
        }
    }
    
    
    public class when_polling_from_bucket1 : using_polling_client
    {
        private IObserveCommits _observeCommits;
        private Task<ICommit> _commitObserved;
        protected override async Task Context()
        {
            await base.Context();
            await StoreEvents.Advanced.CommitMany(4, null, "bucket_2");
            await StoreEvents.Advanced.CommitMany(4, null, "bucket_1");
            _observeCommits = PollingClient.ObserveFromBucket("bucket_1");
            _commitObserved = _observeCommits.FirstAsync().ToTask();
        }

        protected override async Task Because()
        {
            await _observeCommits.PollNow();
        }

        protected override void CleanupSynch()
        {
            _observeCommits.Dispose();
        }

        [Fact]
        public async Task should_observe_commit_from_bucket1()
        {
            var task = await Task.WhenAny(_commitObserved, Task.Delay(PollingInterval * 2));
            Assert.Equal(task, _commitObserved);
            _commitObserved.Result.BucketId.Should().Be("bucket_1");
        }
    }
}