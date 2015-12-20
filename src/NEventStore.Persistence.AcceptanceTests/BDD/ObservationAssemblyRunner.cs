using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NEventStore.Persistence.AcceptanceTests.BDD
{
    public class ObservationAssemblyRunner : XunitTestAssemblyRunner
    {
        public ObservationAssemblyRunner(ITestAssembly testAssembly,
                                         IEnumerable<IXunitTestCase> testCases,
                                         IMessageSink diagnosticMessageSink,
                                         IMessageSink executionMessageSink,
                                         ITestFrameworkExecutionOptions executionOptions)
            : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
        {
            TestCaseOrderer = new ObservationTestCaseOrderer();
        }

        protected override string GetTestFrameworkDisplayName()
        {
            return "Observation Framework";
        }

        protected override string GetTestFrameworkEnvironment()
        {
            return String.Format("{0}-bit .NET {1}", IntPtr.Size * 8, Environment.Version);
        }

        protected override async Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
        {
            var summary = new RunSummary();

            var defaultCases = new List<IXunitTestCase>();
            var observationTestCases = new List<ObservationTestCase>();
            foreach (var testCase in testCases)
            {
                if (testCase is ObservationTestCase)
                    observationTestCases.Add(testCase as ObservationTestCase);
                else
                    defaultCases.Add(testCase);
            }

            if (observationTestCases.Any())
            {
                summary.Aggregate(
                    await new ObservationTestCollectionRunner(
                        testCollection,
                        observationTestCases,
                        DiagnosticMessageSink,
                        messageBus,
                        TestCaseOrderer,
                        new ExceptionAggregator(Aggregator),
                        cancellationTokenSource
                    )
                    .RunAsync()
                );
            }

            if (defaultCases.Any())
            {
                summary.Aggregate(
                    await base.RunTestCollectionAsync(messageBus, testCollection, defaultCases, cancellationTokenSource)
                );
            }

            return summary;
        }
    }
}
