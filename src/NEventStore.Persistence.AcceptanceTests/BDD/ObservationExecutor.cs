using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NEventStore.Persistence.AcceptanceTests.BDD
{
    public class ObservationExecutor : XunitTestFrameworkExecutor
    {
        public ObservationExecutor(AssemblyName assemblyName,
                                   ISourceInformationProvider sourceInformationProvider,
                                   IMessageSink diagnosticMessageSink)
            : base(assemblyName, sourceInformationProvider, diagnosticMessageSink) { }

        protected override ITestFrameworkDiscoverer CreateDiscoverer()
        {
            return new ObservationDiscoverer(AssemblyInfo, SourceInformationProvider, DiagnosticMessageSink);
        }

        protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases,
                                                   IMessageSink executionMessageSink,
                                                   ITestFrameworkExecutionOptions executionOptions)
        {
            var testAssembly = new TestAssembly(AssemblyInfo, AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
            //executionOptions.SetValue("xunit.execution.DisableParallelization", true);
            
            using (var assemblyRunner = new ObservationAssemblyRunner(testAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions))
                await assemblyRunner.RunAsync();

        }
    }
}
