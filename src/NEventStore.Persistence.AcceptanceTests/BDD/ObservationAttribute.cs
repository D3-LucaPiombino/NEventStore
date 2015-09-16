using System;

namespace NEventStore.Persistence.AcceptanceTests.BDD
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ObservationAttribute : Attribute { }
}
