using System;

namespace JetBrains.OsTestFramework
{
    public interface IElevatedCommandResult
    {
        string Output { get; }
        int? ExitCode { get; }
        DateTime? ExitTime { get; }
        bool HasExited { get; }
    }

    public class ElevatedCommandResult : IElevatedCommandResult
    {
        private readonly string _output;
        private readonly int? _exitCode;
        private readonly DateTime? _exitTime;
        private readonly bool _hasExited;

        public ElevatedCommandResult(string output, int? exitCode = null , DateTime? exitTime = null, bool hasExited  = false)
        {
            _output = output;
            _exitCode = exitCode;
            _exitTime = exitTime;
            _hasExited = hasExited;
        }

        public string Output
        {
            get { return _output; }
        }

        public int? ExitCode
        {
            get { return _exitCode; }
        }

        public DateTime? ExitTime
        {
            get { return _exitTime; }
        }

        public bool HasExited
        {
            get { return _hasExited; }
        }
    }
}