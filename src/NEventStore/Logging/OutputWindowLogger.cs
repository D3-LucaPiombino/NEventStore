namespace NEventStore.Logging
{
    using System;
    using System.Diagnostics;

    public class OutputWindowLogger : ILog
    {
        private static readonly object Sync = new object();
        private readonly Type _typeToLog;
        private readonly ISystemTimeProvider _systemTypeProvider;

        public OutputWindowLogger(Type typeToLog, ISystemTimeProvider systemTypeProvider)
        {
            _typeToLog = typeToLog;
            _systemTypeProvider = systemTypeProvider ?? new DefaultSystemTimeProvider();
        }

        public virtual void Verbose(string message, params object[] values)
        {
            DebugWindow("Verbose", message, values);
        }

        public virtual void Debug(string message, params object[] values)
        {
            DebugWindow("Debug", message, values);
        }

        public virtual void Info(string message, params object[] values)
        {
            TraceWindow("Info", message, values);
        }

        public virtual void Warn(string message, params object[] values)
        {
            TraceWindow("Warn", message, values);
        }

        public virtual void Error(string message, params object[] values)
        {
            TraceWindow("Error", message, values);
        }

        public virtual void Fatal(string message, params object[] values)
        {
            TraceWindow("Fatal", message, values);
        }

        protected virtual void DebugWindow(string category, string message, params object[] values)
        {
            lock (Sync)
            {
                System.Diagnostics.Debug.WriteLine(category, message.FormatMessage(_systemTypeProvider, _typeToLog, values));
            }
        }

        protected virtual void TraceWindow(string category, string message, params object[] values)
        {
            lock (Sync)
            {
                Trace.WriteLine(category, message.FormatMessage(_systemTypeProvider, _typeToLog, values));
            }
        }
    }
}