﻿using System.Threading;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace NEventStore.Persistence.AcceptanceTests.BDD
{
    public class ObservationTestCaseRunner : TestCaseRunner<ObservationTestCase>
    {
        readonly string displayName;
        readonly SpecificationBase specification;

        public ObservationTestCaseRunner(SpecificationBase specification,
                                         ObservationTestCase testCase,
                                         string displayName,
                                         IMessageBus messageBus,
                                         ExceptionAggregator aggregator,
                                         CancellationTokenSource cancellationTokenSource)
            : base(testCase, messageBus, aggregator, cancellationTokenSource)
        {
            this.specification = specification;
            this.displayName = displayName;
        }

        protected override Task<RunSummary> RunTestAsync()
        {
            var timer = new ExecutionTimer();
            var TestClass = TestCase.TestMethod.TestClass.Class.ToRuntimeType();
            var TestMethod = TestCase.TestMethod.Method.ToRuntimeMethod();
            var test = new ObservationTest(TestCase, displayName);

            return new ObservationTestRunner(specification, test, MessageBus, timer, TestClass, TestMethod, Aggregator, CancellationTokenSource).RunAsync();
        }
    }
}

