using System;

namespace JetBrains.OsTestFramework.Common
{
    public interface ITestLoggerWriter
    {
        void Write(string formatLogMessage);
    }

    public class ConsoleTestLoggerWriter : ITestLoggerWriter
    {
        public void Write(string formatLogMessage)
        {
            Console.WriteLine(formatLogMessage);
        }
    }
}