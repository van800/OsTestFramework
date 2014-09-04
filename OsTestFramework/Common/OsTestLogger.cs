using System;

namespace JetBrains.OsTestFramework.Common
{
    public static class OsTestLogger
    {
        private static ITestLoggerWriter _loggingAction;

        static OsTestLogger()
        {
            _loggingAction = new ConsoleTestLoggerWriter();
        }

        public static void WriteLine(string content)
        {
            _loggingAction.Write(FormatLogMessage(content));
        }

        public static void SetLoggingAction(ITestLoggerWriter loggingAction)
        {
            _loggingAction = loggingAction;
        }

        private static string FormatLogMessage(string message)
        {
            return DateTime.Now.ToString("HH:mm:ss") + " " + message;
        }
    }
}