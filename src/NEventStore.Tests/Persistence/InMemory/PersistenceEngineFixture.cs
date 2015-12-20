// ReSharper disable once CheckNamespace
namespace NEventStore.Persistence.AcceptanceTests
{
    using NEventStore.Persistence.InMemory;

    public partial class PersistenceEngineFixture
    {
        public PersistenceEngineFixture()
        {
            _createPersistence = (_, systemTimeProvider) =>
                new InMemoryPersistenceEngine();
        }
    }


    //public partial class PersistenceEngineConcern
    //{
    //    public PersistenceEngineConcern()
    //    {
    //        _createPersistence = (_, systemTimeProvider) =>
    //            new InMemoryPersistenceEngine();
    //    }
    //}
}