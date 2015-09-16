using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NEventStore.Persistence.AcceptanceTests.BDD
{
    public class ObservationTestFramework : TestFramework
    {
        public ObservationTestFramework(IMessageSink diagnosticMessageSink)
            : base(diagnosticMessageSink) { }

        protected override ITestFrameworkDiscoverer CreateDiscoverer(IAssemblyInfo assemblyInfo)
        {
            return new ObservationDiscoverer(assemblyInfo, SourceInformationProvider, DiagnosticMessageSink);
        }

        protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        {
            return new ObservationExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
        }
    }
}
