using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NEventStore.Persistence.AcceptanceTests.BDD
{
    public class ObservationTestCollectionRunner : TestCollectionRunner<ObservationTestCase>
    {
        readonly static RunSummary FailedSummary = new RunSummary { Total = 1, Failed = 1 };

        readonly IMessageSink diagnosticMessageSink;

        readonly IDictionary<Type, object> collectionFixtureMappings = new Dictionary<Type, object>();



        public ObservationTestCollectionRunner(ITestCollection testCollection,
                                               IEnumerable<ObservationTestCase> testCases,
                                               IMessageSink diagnosticMessageSink,
                                               IMessageBus messageBus,
                                               ITestCaseOrderer testCaseOrderer,
                                               ExceptionAggregator aggregator,
                                               CancellationTokenSource cancellationTokenSource)
            : base(testCollection, testCases, messageBus, testCaseOrderer, aggregator, cancellationTokenSource)
        {
            this.diagnosticMessageSink = diagnosticMessageSink;
        }

        /// <summary>
        /// Gets the fixture mappings that were created during <see cref="AfterTestClassStartingAsync"/>.
        /// </summary>
        protected Dictionary<Type, object> ClassFixtureMappings { get; set; } = new Dictionary<Type, object>();

        /// <summary>
        /// Creates the instance of a class fixture type to be used by the test class. If the fixture can be created,
        /// it should be placed into the <see cref="ClassFixtureMappings"/> dictionary; if it cannot, then the method
        /// should record the error by calling <code>Aggregator.Add</code>.
        /// </summary>
        /// <param name="fixtureType">The type of the fixture to be created</param>
        protected void CreateClassFixture(Type fixtureType)
        {
            var ctors = fixtureType
                .GetConstructors()
                .Where(ci => !ci.IsStatic && ci.IsPublic)
                .ToList();

            if (ctors.Count != 1)
            {
                Aggregator.Add(new TestClassException($"Class fixture type '{fixtureType.FullName}' may only define a single public constructor."));
                return;
            }

            var ctor = ctors[0];
            var missingParameters = new List<ParameterInfo>();
            var ctorArgs = ctor
                .GetParameters()
                .Select(p =>
                {
                    object arg;
                    if (!collectionFixtureMappings.TryGetValue(p.ParameterType, out arg))
                        missingParameters.Add(p);
                    return arg;
                })
                .ToArray();

            if (missingParameters.Count > 0)
            {
                Aggregator.Add(new TestClassException(
                    $"Class fixture type '{fixtureType.FullName}' had one or more unresolved constructor arguments: {string.Join(", ", missingParameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))}"
                ));
            }
            else
            {
                Aggregator.Run(() => ClassFixtureMappings[fixtureType] = ctor.Invoke(ctorArgs));
            }
        }

        /// <inheritdoc/>
        protected void SetTestCaseOrderer(IReflectionTypeInfo Class)
        {
            var ordererAttribute = Class.GetCustomAttributes(typeof(TestCaseOrdererAttribute)).SingleOrDefault();
            if (ordererAttribute != null)
            {
                try
                {
                    var testCaseOrderer = ExtensibilityPointFactory.GetTestCaseOrderer(diagnosticMessageSink, ordererAttribute);
                    if (testCaseOrderer != null)
                        TestCaseOrderer = testCaseOrderer;
                    else
                    {
                        var args = ordererAttribute.GetConstructorArguments().Cast<string>().ToList();
                        diagnosticMessageSink.OnMessage(new DiagnosticMessage($"Could not find type '{args[0]}' in {args[1]} for class-level test case orderer on test class '{Class.Name}'"));
                    }
                }
                catch (Exception ex)
                {
                    var innerEx = ex.InnerException ?? ex; //.Unwrap();
                    var args = ordererAttribute.GetConstructorArguments().Cast<string>().ToList();
                    diagnosticMessageSink.OnMessage(new DiagnosticMessage($"Class-level test case orderer '{args[0]}' for test class '{Class.Name}' threw '{innerEx.GetType().FullName}' " +
                        $"during construction: {innerEx.Message}{Environment.NewLine}{innerEx.StackTrace}"));
                }
            }
        }

        protected MethodInfo SelectTestClassFixtureInjectionMethod(IReflectionTypeInfo Class)
        {
            var methods = Class
                .Type
                .GetTypeInfo()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(ci => !ci.IsGenericMethod && ci.Name == "SetFixture")
                .ToList();
            
            var methodGroup = methods
               .GroupBy(ci => ci.GetParameters().Length)
               .OrderByDescending(g => g.Count())
               .FirstOrDefault();

            if (methodGroup == null)
                return null;
            if(methodGroup.Count() > 1)
            {
                Aggregator.Add(
                    new TestClassException(
                        $"Ambiguous SetFixture method on test class '{Class.Type.FullName}'. " +
                        "Remember that the overload with more parameters is selected."
                    )
                );
                return null;
            }
            return methods
                .OrderByDescending(ci => ci.GetParameters().Length)
                .FirstOrDefault();
        }

        protected bool TryGetFixtureInjectionMethodArgument(MethodInfo constructor, int index, ParameterInfo parameter, out object argumentValue)
        {
            if (parameter.ParameterType == typeof(ITestOutputHelper))
            {
                argumentValue = new TestOutputHelper();
                return true;
            }
            return ClassFixtureMappings.TryGetValue(parameter.ParameterType, out argumentValue)
                || collectionFixtureMappings.TryGetValue(parameter.ParameterType, out argumentValue);
        }

        private async Task InitializeAsyncLifeTime(params object[] instances)
        {
            foreach (var uninitializedInstance in instances.OfType<IAsyncLifetime>())
                await Aggregator.RunAsync(uninitializedInstance.InitializeAsync);
        }

        protected async Task Initialize(IReflectionTypeInfo @class, object instance)
        {
            var method = SelectTestClassFixtureInjectionMethod(@class);

            // No method available.
            // Do nothing :D
            if (method == null)
            {
                // We need to initialize at least the test class
                await InitializeAsyncLifeTime(instance);
                return;
            }

            
            // We will create a singleton for each type
            var fixtureTypes = method
                .GetParameters()
                // This is the same as "Distinct By ParameterType"
                .Select(p => p.ParameterType)
                .GroupBy(type => type)
                .SelectMany(group => group.Take(1));

            foreach (var fixtureType in fixtureTypes)
            {
                CreateClassFixture(fixtureType);
            }

            // Initialize the test class last
            await InitializeAsyncLifeTime(ClassFixtureMappings.Values.Concat(new[] { instance }).ToArray());


            var parameters = method.GetParameters();
            var parameterValues = new List<object>();
            var missingParameters = new List<ParameterInfo>();
            foreach (var parameter in parameters)
            {
                object value;
                if(!TryGetFixtureInjectionMethodArgument(method, 0, parameter, out value))
                {
                    missingParameters.Add(parameter);
                }
                else
                {
                    parameterValues.Add(value);
                }
            }
            if (missingParameters.Count > 0)
            {
                Aggregator.Add(new TestClassException(
                    $"Class fixture type '{@class.Type.FullName}' had one or more unresolved constructor arguments: {string.Join(", ", missingParameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))}"
                ));
                return;
            }
            var result = method.Invoke(instance, parameterValues.ToArray());
            var task = result as Task;
            if (task != null)
                await task;

        }

        
        protected async Task Finalize(object specification)
        {
            // Firrst dispose the test class 
            var values = new[] { specification }.Concat(ClassFixtureMappings.Values);

            foreach (var item in values)
            {
                var a = item as IAsyncLifetime;
                if (a != null)
                    await a.DisposeAsync();
                var d = item as IDisposable;
                if (d != null)
                    d.Dispose();
            }
        }


        protected override async Task<RunSummary> RunTestClassAsync(ITestClass testClass,
                                                                    IReflectionTypeInfo @class,
                                                                    IEnumerable<ObservationTestCase> testCases)
        {
            var timer = new ExecutionTimer();
            var classType = testClass.Class.ToRuntimeType();

            Aggregator.Run(() => SetTestCaseOrderer(@class));

            var specification = Activator.CreateInstance(classType) as SpecificationBase;

            if (specification == null)
            {
                Aggregator.Add(new InvalidOperationException(string.Format("Test class {0} cannot be static, and must derive from Specification.",
                    testClass.Class.Name)));
                return FailedSummary;
            }

            await Aggregator.RunAsync(() => Initialize(@class, specification));

            if (Aggregator.HasExceptions)
            {
                return FailedSummary;
            }

            await Aggregator.RunAsync(specification.OnStart);

            var result = await new ObservationTestClassRunner(
                specification, 
                testClass, 
                @class, 
                testCases, 
                diagnosticMessageSink, 
                MessageBus, 
                TestCaseOrderer, 
                new ExceptionAggregator(Aggregator), CancellationTokenSource
            )
            .RunAsync();

            await Aggregator.RunAsync(specification.OnFinish);

            await Aggregator.RunAsync(() => Finalize(specification));

            return result;
        }
    }
}
