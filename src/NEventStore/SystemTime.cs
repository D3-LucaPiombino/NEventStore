namespace NEventStore
{
    using System;

    /// <summary>
    /// Provides the the current moment in time.
    /// </summary>
    public interface ISystemTimeProvider
    {
        DateTime UtcNow { get; }
    }

    public class DefaultSystemTimeProvider : ISystemTimeProvider
    {
        public DateTime UtcNow
        {
            get
            {
                return DateTime.UtcNow;
            }
        }
    }

}