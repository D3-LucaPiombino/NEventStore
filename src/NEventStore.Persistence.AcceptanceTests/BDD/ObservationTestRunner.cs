using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NEventStore.Persistence.AcceptanceTests.BDD
{
    public class ObservationTestRunner : TestRunner<ObservationTestCase>
    {
        readonly SpecificationBase specification;
        readonly ExecutionTimer timer;
        
        public ObservationTestRunner(SpecificationBase specification,
                                     ITest test,
                                     IMessageBus messageBus,
                                     ExecutionTimer timer,
                                     Type testClass,
                                     MethodInfo testMethod,
                                     ExceptionAggregator aggregator,
                                     CancellationTokenSource cancellationTokenSource)
            : base(test, messageBus, testClass, null, testMethod, null, null, aggregator, cancellationTokenSource)
        {
            this.specification = specification;
            this.timer = timer;
        }

        protected override async Task<Tuple<decimal, string>> InvokeTestAsync(ExceptionAggregator aggregator)
        {
            var duration = await new ObservationTestInvoker(specification, Test, MessageBus, TestClass, TestMethod, aggregator, CancellationTokenSource).RunAsync();
            return Tuple.Create(duration, String.Empty);
        }
    }
}

