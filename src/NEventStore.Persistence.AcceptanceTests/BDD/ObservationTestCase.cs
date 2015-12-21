using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NEventStore.Persistence.AcceptanceTests.BDD
{
    public class ObservationTestCase : TestMethodTestCase, IXunitTestCase
    {
        [Obsolete("For de-serialization purposes only", error: true)]
        public ObservationTestCase() { }

        public ObservationTestCase(TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod)
            : base(defaultMethodDisplay, testMethod) { }

        protected override void Initialize()
        {
            base.Initialize();

            DisplayName = String.Format("{0}, it {1}", TestMethod.TestClass.Class.Name, TestMethod.Method.Name).Replace('_', ' ');
        }

        public virtual Task<RunSummary> RunAsync(SpecificationBase specification,
                                         IMessageBus messageBus,
                                         ExceptionAggregator aggregator,
                                         CancellationTokenSource cancellationTokenSource)
        {
            return new ObservationTestCaseRunner(specification, this, DisplayName, messageBus, aggregator, cancellationTokenSource).RunAsync();
        }

        public Task<RunSummary> RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            throw new NotImplementedException();
        }
    }


    public class SkippedTestCase : ObservationTestCase
    {
        private string _skipReason = "";

        [Obsolete("For de-serialization purposes only", error: true)]
        public SkippedTestCase() { }

        public SkippedTestCase(TestMethodDisplay defaultMethodDisplay, ITestMethod testMethod)
            : base(defaultMethodDisplay, testMethod)
        { }

        public void SetSkipReason(string skipReason)
        {
            _skipReason = skipReason;
        }

        protected override void Initialize()
        {
            base.Initialize();
            //[Skip({2})]
            var name = $"{TestMethod.TestClass.Class.Name}, it {TestMethod.Method.Name}".Replace('_', ' ');
            DisplayName = $"[Skipped: {_skipReason}] {name}";
        }

        public override Task<RunSummary> RunAsync(SpecificationBase specification,
                                         IMessageBus messageBus,
                                         ExceptionAggregator aggregator,
                                         CancellationTokenSource cancellationTokenSource)
        {
            return Task.FromResult(new RunSummary { Total = 1, Skipped = 1  });
        }

        
    }
}