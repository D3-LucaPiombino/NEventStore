namespace NEventStore.Logging
{
    using System;

    public class ConsoleWindowLogger : ILog
    {
        private static readonly object Sync = new object();
        private readonly ConsoleColor _originalColor = Console.ForegroundColor;
        private readonly Type _typeToLog;
        private readonly ISystemTimeProvider _systemTypeProvider;

        public ConsoleWindowLogger(Type typeToLog, ISystemTimeProvider systemTypeProvider)
        {
            _systemTypeProvider = systemTypeProvider ?? new DefaultSystemTimeProvider();
            _typeToLog = typeToLog;

        }

        public virtual void Verbose(string message, params object[] values)
        {
            Log(ConsoleColor.DarkGreen, message, values);
        }

        public virtual void Debug(string message, params object[] values)
        {
            Log(ConsoleColor.Green, message, values);
        }

        public virtual void Info(string message, params object[] values)
        {
            Log(ConsoleColor.White, message, values);
        }

        public virtual void Warn(string message, params object[] values)
        {
            Log(ConsoleColor.Yellow, message, values);
        }

        public virtual void Error(string message, params object[] values)
        {
            Log(ConsoleColor.DarkRed, message, values);
        }

        public virtual void Fatal(string message, params object[] values)
        {
            Log(ConsoleColor.Red, message, values);
        }

        private void Log(ConsoleColor color, string message, params object[] values)
        {
            lock (Sync)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message.FormatMessage(_systemTypeProvider, _typeToLog,  values));
                Console.ForegroundColor = _originalColor;
            }
        }
    }
}