using System;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NEventStore.Persistence.AcceptanceTests.BDD
{
    public class ObservationDiscoverer : XunitTestFrameworkDiscoverer
    {
        //readonly CollectionPerClassTestCollectionFactory testCollectionFactory;

        public ObservationDiscoverer(IAssemblyInfo assemblyInfo,
                                     ISourceInformationProvider sourceProvider,
                                     IMessageSink diagnosticMessageSink)
            : base(assemblyInfo, sourceProvider, diagnosticMessageSink)
        {
            //var testAssembly = new TestAssembly(assemblyInfo, AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
            //testCollectionFactory = new CollectionPerClassTestCollectionFactory(testAssembly, diagnosticMessageSink);

            //_xunitDiscoverer = new XunitTestFrameworkDiscoverer(assemblyInfo, sourceProvider, diagnosticMessageSink, testCollectionFactory);
        }

        

        protected override ITestClass CreateTestClass(ITypeInfo @class)
        {
            return new TestClass(TestCollectionFactory.Get(@class), @class);
        }
        protected virtual string GetSkipReason(IAttributeInfo factAttribute)
            => factAttribute.GetNamedArgument<string>("Skip");

        bool FindTestsForMethod(ITestMethod testMethod,
                                TestMethodDisplay defaultMethodDisplay,
                                bool includeSourceInformation,
                                IMessageBus messageBus)
        {
            var observationAttribute = testMethod.Method.GetCustomAttributes(typeof(FactAttribute)).FirstOrDefault(); 
            if (observationAttribute == null)
                return true;

            var testCase = new ObservationTestCase(defaultMethodDisplay, testMethod);

            var skipReason = GetSkipReason(observationAttribute);
            if (!string.IsNullOrWhiteSpace(skipReason))
            {
                var skipTestCase = new SkippedTestCase(defaultMethodDisplay, testMethod);
                skipTestCase.SetSkipReason(skipReason);
                testCase = skipTestCase;
            }
            
            if (!ReportDiscoveredTestCase(testCase, includeSourceInformation, messageBus))
                return false;

            return true;
        }

      

        bool IsSpecificationClass(ITypeInfo typeInfo)
        {
            var type = typeInfo.ToRuntimeType();
            while (type != null)
            {
                DiagnosticMessageSink.OnMessage(new DiagnosticMessage("Test report."));
                if (type == typeof(SpecificationBase))
                    return true;
                type = type.BaseType;
            }
            return false;
        }

        protected override bool FindTestsForType(ITestClass testClass,
                                                 bool includeSourceInformation,
                                                 IMessageBus messageBus,
                                                 ITestFrameworkDiscoveryOptions discoveryOptions)
        {
            if(!IsSpecificationClass(testClass.Class))
            {
                return base.FindTestsForType(testClass, includeSourceInformation, messageBus, discoveryOptions);
            }

            var methodDisplay = discoveryOptions.MethodDisplayOrDefault();

            foreach (var method in testClass.Class.GetMethods(includePrivateMethods: true))
                if (!FindTestsForMethod(new TestMethod(testClass, method), methodDisplay, includeSourceInformation, messageBus))
                    return false;

            return true;
        }
    }
}
