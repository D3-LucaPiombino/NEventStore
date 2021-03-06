﻿
#pragma warning disable 169
// ReSharper disable InconsistentNaming

namespace NEventStore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using NEventStore.Persistence;
    using NEventStore.Persistence.AcceptanceTests.BDD;
    using Xunit;
    using ALinq;
    using NSubstitute;
    public class PipelineHooksAwarePersistenceDecoratorTests
    {
        public class when_disposing_the_decorator : using_underlying_persistence
        {
            protected override Task Because()
            {
                Decorator.Dispose();
				return Task.FromResult(true);
            }

            [Fact]
            public void should_dispose_the_underlying_persistence()
            {
                //A.CallTo(() => persistence.Dispose()).MustHaveHappened(Repeated.Exactly.Once);
                persistence.Received(1).Dispose();
            }
        }

        public class when_reading_the_all_events_from_date : using_underlying_persistence
        {
            private ICommit _commit;
            private DateTime _date;
            private IPipelineHook _hook1;
            private IPipelineHook _hook2;

            protected override Task Context()
            {
                _date = DateTime.Now;
                _commit = new Commit(Bucket.Default, streamId, 1, Guid.NewGuid(), 1, DateTime.Now, new LongCheckpoint(0).Value, null, null);

                //_hook1 = A.Fake<IPipelineHook>();
                //A.CallTo(() => _hook1.Select(_commit)).Returns(_commit);
                _hook1 = Substitute.For<IPipelineHook>();
                _hook1.Select(_commit).Returns(_commit);
                pipelineHooks.Add(_hook1);

                //_hook2 = A.Fake<IPipelineHook>();
                //A.CallTo(() => _hook2.Select(_commit)).Returns(_commit);
                _hook2 = Substitute.For<IPipelineHook>();
                _hook2.Select(_commit).Returns(_commit);
                pipelineHooks.Add(_hook2);

                //A.CallTo(() => persistence.GetFrom(Bucket.Default, _date)).Returns(new List<ICommit> {_commit}.AsAsyncEnumerable());
                persistence.GetFrom(Bucket.Default, _date).Returns(new List<ICommit> { _commit }.AsAsyncEnumerable());
                return Task.FromResult(true);
            }

            protected override async Task Because()
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                // Forces enumeration of commits.
                var commits = Decorator.GetFrom(_date);
				var items = await commits.ToList();
            }

            [Fact]
            public void should_call_the_underlying_persistence_to_get_events()
            {
                //A.CallTo(() => persistence.GetFrom(Bucket.Default, _date)).MustHaveHappened(Repeated.Exactly.Once);
                persistence.Received(1).GetFrom(Bucket.Default, _date);
            }

            [Fact]
            public void should_pass_all_events_through_the_pipeline_hooks()
            {
                //A.CallTo(() => _hook1.Select(_commit)).MustHaveHappened(Repeated.Exactly.Once);
                //A.CallTo(() => _hook2.Select(_commit)).MustHaveHappened(Repeated.Exactly.Once);
                _hook1.Received(1).Select(_commit);
                _hook2.Received(1).Select(_commit);
            }
        }

        public class when_getting_the_all_events_from_min_to_max_revision : using_underlying_persistence
        {
            private ICommit _commit;
            private DateTime _date;
            private IPipelineHook _hook1;
            private IPipelineHook _hook2;

            protected override Task Context()
            {
                _date = DateTime.Now;
                _commit = new Commit(Bucket.Default, streamId, 1, Guid.NewGuid(), 1, DateTime.Now, new LongCheckpoint(0).Value, null, null);

                //_hook1 = A.Fake<IPipelineHook>();
                //A.CallTo(() => _hook1.Select(_commit)).Returns(_commit);
                _hook1 = Substitute.For<IPipelineHook>();
                _hook1.Select(_commit).Returns(_commit);
                pipelineHooks.Add(_hook1);

                //_hook2 = A.Fake<IPipelineHook>();
                //A.CallTo(() => _hook2.Select(_commit)).Returns(_commit);
                _hook2 = Substitute.For<IPipelineHook>();
                _hook2.Select(_commit).Returns(_commit);
                pipelineHooks.Add(_hook2);

                //A.CallTo(() => persistence.GetFrom(Bucket.Default, _commit.StreamId, 0, int.MaxValue))
                //    .Returns(new List<ICommit> { _commit }.ToAsync());
                persistence.GetFrom(Bucket.Default, _commit.StreamId, 0, int.MaxValue)
                    .Returns(new List<ICommit> { _commit }.ToAsync());
                return Task.FromResult(true);
            }

            protected override async Task Because()
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                // Forces enumeration of commits.
                var commit = Decorator.GetFrom(Bucket.Default, _commit.StreamId, 0, int.MaxValue);
				var commits = await commit.ToList();
            }

            [Fact]
            public void should_call_the_underlying_persistence_to_get_events()
            {
                //A.CallTo(() => persistence.GetFrom(Bucket.Default, _commit.StreamId, 0, int.MaxValue)).MustHaveHappened(Repeated.Exactly.Once);
                persistence.Received(1).GetFrom(Bucket.Default, _commit.StreamId, 0, int.MaxValue);
            }

            [Fact]
            public void should_pass_all_events_through_the_pipeline_hooks()
            {
                //A.CallTo(() => _hook1.Select(_commit)).MustHaveHappened(Repeated.Exactly.Once);
                //A.CallTo(() => _hook2.Select(_commit)).MustHaveHappened(Repeated.Exactly.Once);
                _hook1.Received(1).Select(_commit);
                _hook2.Received(1).Select(_commit);
            }
        }

        public class when_getting_all_events_from_to : using_underlying_persistence
        {
            private ICommit _commit;
            private DateTime _end;
            private IPipelineHook _hook1;
            private IPipelineHook _hook2;
            private DateTime _start;

            protected override Task Context()
            {
                _start = DateTime.Now;
                _end = DateTime.Now;
                _commit = new Commit(Bucket.Default, streamId, 1, Guid.NewGuid(), 1, DateTime.Now, new LongCheckpoint(0).Value, null, null);


                _hook1 = Substitute.For<IPipelineHook>();
                _hook1.Select(_commit).Returns(_commit);
                pipelineHooks.Add(_hook1);

                _hook2 = Substitute.For<IPipelineHook>();
                _hook2.Select(_commit).Returns(_commit);
                pipelineHooks.Add(_hook2);

                //_hook1 = A.Fake<IPipelineHook>();
                //A.CallTo(() => _hook1.Select(_commit)).Returns(_commit);
                //pipelineHooks.Add(_hook1);

                //_hook2 = A.Fake<IPipelineHook>();
                //A.CallTo(() => _hook2.Select(_commit)).Returns(_commit);
                //pipelineHooks.Add(_hook2);

                //A.CallTo(() => persistence.GetFromTo(Bucket.Default, _start, _end)).Returns(new List<ICommit> {_commit}.ToAsync());
                persistence.GetFromTo(Bucket.Default, _start, _end).Returns(new List<ICommit> { _commit }.ToAsync());
                return Task.FromResult(true);
            }

            protected override async Task Because()
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                // Forces enumeration of commits
                await (Decorator.GetFromTo(_start, _end)).ToList();
            }

            [Fact]
            public void should_call_the_underlying_persistence_to_get_events()
            {
                //A.CallTo(() => persistence.GetFromTo(Bucket.Default, _start, _end)).MustHaveHappened(Repeated.Exactly.Once);
                persistence.Received(1).GetFromTo(Bucket.Default, _start, _end);
            }

            [Fact]
            public void should_pass_all_events_through_the_pipeline_hooks()
            {
                _hook1.Received(1).Select(_commit);
                _hook2.Received(1).Select(_commit);
            }
        }

        public class when_committing : using_underlying_persistence
        {
            private CommitAttempt _attempt;

            protected override Task Context()
            {
                _attempt = new CommitAttempt(streamId, 1, Guid.NewGuid(), 1, DateTime.Now, null, new List<EventMessage> {new EventMessage()});
                return Task.FromResult(true);
            }

            protected override async Task Because()
            {
                await Decorator.Commit(_attempt);
            }

            [Fact]
            public void should_dispose_the_underlying_persistence()
            {
                //A.CallTo(() => persistence.Commit(_attempt)).MustHaveHappened(Repeated.Exactly.Once);
                persistence.Received(1).Commit(_attempt);
            }
        }

        public class when_reading_the_all_events_from_checkpoint : using_underlying_persistence
        {
            private ICommit _commit;
            private IPipelineHook _hook1;
            private IPipelineHook _hook2;

            protected override Task Context()
            {
                _commit = new Commit(Bucket.Default, streamId, 1, Guid.NewGuid(), 1, DateTime.Now, new LongCheckpoint(0).Value, null, null);

                //_hook1 = A.Fake<IPipelineHook>();
                //A.CallTo(() => _hook1.Select(_commit)).Returns(_commit);
                //pipelineHooks.Add(_hook1);

                //_hook2 = A.Fake<IPipelineHook>();
                //A.CallTo(() => _hook2.Select(_commit)).Returns(_commit);
                //pipelineHooks.Add(_hook2);

                _hook1 = Substitute.For<IPipelineHook>();
                _hook1.Select(_commit).Returns(_commit);
                pipelineHooks.Add(_hook1);

                _hook2 = Substitute.For<IPipelineHook>();
                _hook2.Select(_commit).Returns(_commit);
                pipelineHooks.Add(_hook2);

                //A.CallTo(() => persistence.GetFrom(null)).Returns(new List<ICommit> {_commit}.ToAsync());
                persistence.GetFrom(null).Returns(new List<ICommit> { _commit }.ToAsync());
                return Task.FromResult(true);
            }

            protected override async Task Because()
            {
                await (Decorator.GetFrom(null)).ToList();
            }

            [Fact]
            public void should_call_the_underlying_persistence_to_get_events()
            {
                //A.CallTo(() => persistence.GetFrom(null)).MustHaveHappened(Repeated.Exactly.Once);
                persistence.Received(1).GetFrom(null);
            }

            [Fact]
            public void should_pass_all_events_through_the_pipeline_hooks()
            {
                _hook1.Received(1).Select(_commit);
                _hook2.Received(1).Select(_commit);
            }
        }

        public class when_purging : using_underlying_persistence
        {
            private IPipelineHook _hook;

            protected override Task Context()
            {
                _hook = Substitute.For<IPipelineHook>();
                pipelineHooks.Add(_hook);
                return Task.FromResult(true);
            }

            protected override async Task Because()
            {
                await Decorator.Purge();
            }

            [Fact]
            public void should_call_the_pipeline_hook_purge()
            {
                //A.CallTo(() => _hook.OnPurge(null)).MustHaveHappened(Repeated.Exactly.Once);
                _hook.Received(1).OnPurge(null);
            }
        }

        public class when_purging_a_bucket : using_underlying_persistence
        {
            private IPipelineHook _hook;
            private const string _bucketId = "Bucket";

            protected override Task Context()
            {
                _hook = Substitute.For<IPipelineHook>();
                pipelineHooks.Add(_hook);
                return Task.FromResult(true);
            }

            protected override async Task Because()
            {
                await Decorator.Purge(_bucketId);
            }

            [Fact]
            public void should_call_the_pipeline_hook_purge()
            {
                //A.CallTo(() => _hook.OnPurge(_bucketId)).MustHaveHappened(Repeated.Exactly.Once);
                _hook.Received(1).OnPurge(_bucketId);
            }
        }

        public class when_deleting_a_stream : using_underlying_persistence
        {
            private IPipelineHook _hook;
            private const string _bucketId = "Bucket";
            private const string _streamId = "Stream";

            protected override Task Context()
            {
                _hook = Substitute.For<IPipelineHook>();
                pipelineHooks.Add(_hook);
                return Task.FromResult(true);
            }

            protected override async Task Because()
            {
                await Decorator.DeleteStream(_bucketId, _streamId);
            }

            [Fact]
            public void should_call_the_pipeline_hook_purge()
            {
                //A.CallTo(() => _hook.OnDeleteStream(_bucketId, _streamId)).MustHaveHappened(Repeated.Exactly.Once);
                _hook.Received(1).OnDeleteStream(_bucketId, _streamId);
            }
        }

        public abstract class using_underlying_persistence : SpecificationBase
        {
            private PipelineHooksAwarePersistanceDecorator decorator;
            protected readonly IPersistStreams persistence = Substitute.For<IPersistStreams>();
            protected readonly List<IPipelineHook> pipelineHooks = new List<IPipelineHook>();
            protected readonly string streamId = Guid.NewGuid().ToString();

            public PipelineHooksAwarePersistanceDecorator Decorator
            {
                get { return decorator ?? (decorator = new PipelineHooksAwarePersistanceDecorator(persistence, pipelineHooks.Select(x => x))); }
                set { decorator = value; }
            }
        }
    }
}

// ReSharper enable InconsistentNaming
#pragma warning restore 169