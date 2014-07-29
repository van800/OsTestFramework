using System;

namespace JetBrains.OsTestFramework.Common
{
    public static class OsTestLogger
    {
        private static Action<string> _loggingAction;

        static OsTestLogger()
        {
            _loggingAction = s => Console.WriteLine(s);
        }

        public static void WriteLine(string content)
        {
            _loggingAction.Invoke(FormatLogMessage(content));
        }

        public static void SetLoggingAction(Action<string> loggingAction)
        {
            _loggingAction = loggingAction;
        }

        private static string FormatLogMessage(string s)
        {
            return DateTime.Now.ToString("HH:mm:ss") + " " + s;
        }
    }
}