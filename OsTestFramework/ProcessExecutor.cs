using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using JetBrains.OsTestFramework.Common;

namespace JetBrains.OsTestFramework
{
    public interface IProcessExecutor
    {
        IElevatedCommandResult Execute();
    }

    public class ProcessExecutor : IDisposable, IProcessExecutor
    {
        private readonly StringBuilder _error;
        private readonly AutoResetEvent _errorWaitHandle;
        private readonly TimeSpan? _executionTimeout;
        private readonly StringBuilder _output;
        private readonly RemoteEnvironment _remoteEnvironment;
        private AutoResetEvent _outputWaitHandle;
        private Process _process;
        private IAsRemoteUserScopeExecutor _asRemoteUserScopeExecutor;

        public ProcessExecutor(RemoteEnvironment remoteEnvironment, string commandType, string psExecArg, string command,
            string[] args, TimeSpan startTimeout, IAsRemoteUserScopeExecutor asRemoteUserScopeExecutor, TimeSpan? executionTimeout = null, bool interactWithDesktop = true)
        {
            _executionTimeout = executionTimeout;
            _asRemoteUserScopeExecutor = asRemoteUserScopeExecutor;
            _remoteEnvironment = remoteEnvironment;

            string arguments = GenerateArguments(psExecArg, command, args, startTimeout, interactWithDesktop);

            _process = new Process
            {
                StartInfo = CreateStartInfo(remoteEnvironment.PsExecPath, arguments)
            };

            _output = new StringBuilder();
            _error = new StringBuilder();

            OsTestLogger.WriteLine("Running " + commandType + "ElevatedCommandInGuest: " +
                                   _process.StartInfo.FileName + " " + _process.StartInfo.Arguments);
            _outputWaitHandle = new AutoResetEvent(false);
            _errorWaitHandle = new AutoResetEvent(false);
            RegisterToEvents();
        }

        public void Dispose()
        {
            UnregisterFromEvents();

            if (_process != null)
            {
                _process.Dispose();
                _process = null;
            }
            if (_errorWaitHandle != null)
            {
                _errorWaitHandle.Close();
                _outputWaitHandle = null;
            }
            if (_outputWaitHandle != null)
            {
                _outputWaitHandle.Close();
                _outputWaitHandle = null;
            }
        }

        public IElevatedCommandResult Execute()
        {
            _process.Start();

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            int milliseconds = _executionTimeout.HasValue
                ? (int) _executionTimeout.Value.TotalMilliseconds
                : Int32.MaxValue;

            string result;
            if (_process.WaitForExit(milliseconds) &&
                _outputWaitHandle.WaitOne(milliseconds) &&
                _errorWaitHandle.WaitOne(milliseconds))
            {
                result = ("StdErr:" + _error + "StdOut:" + _output).Replace("\r", " ").Replace("\n", " ");
                OsTestLogger.WriteLine(result);
                return new ElevatedCommandResult(result, _process.ExitCode, _process.ExitTime, _process.HasExited);
            }

            result = ("StdErr:" + _error + "StdOut:" + _output).Replace("\r", " ").Replace("\n", " ");
            OsTestLogger.WriteLine(result);
            return new ElevatedCommandResult(result);
        }

        private static ProcessStartInfo CreateStartInfo(string psExecPath, string arguments)
        {
            return new ProcessStartInfo
            {
                FileName = psExecPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ErrorDialog = false,
                CreateNoWindow = true
            };
        }

        private void RegisterToEvents()
        {
            _process.OutputDataReceived += process_OutputDataReceived;
            _process.ErrorDataReceived += process_ErrorDataReceived;
        }

        private void UnregisterFromEvents()
        {
            _process.OutputDataReceived -= process_OutputDataReceived;
            _process.ErrorDataReceived -= process_ErrorDataReceived;
        }

        private void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                _errorWaitHandle.Set();
            }
            else
            {
                _error.AppendLine(e.Data);
            }
        }

        private void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                _outputWaitHandle.Set();
            }
            else
            {
                _output.AppendLine(e.Data);
            }
        }

        private string GenerateArguments(string psExecArg, string command, string[] args, TimeSpan startTimeout,
            bool interactWithDesktop)
        {
            string arg = "";
            if (args != null)
            {
                arg = args.Aggregate((a, b) => string.Format("{0} {1}", a, b));
            }

            StringBuilder argumentsBuilder = new StringBuilder()
                .AppendFormat(@"-accepteula \\{0}", _remoteEnvironment.IpAddress)
                .Append(" -H ");

            if (interactWithDesktop)
            {
                argumentsBuilder.Append(" -I ");
            }

            _asRemoteUserScopeExecutor.Execute(_remoteEnvironment.IpAddress, _remoteEnvironment.UserName, _remoteEnvironment.Password,
                () => argumentsBuilder.AppendFormat(@"{0} -N {1} {2} {3}", psExecArg, startTimeout.TotalSeconds, command, args));

            return argumentsBuilder.ToString();
        }
    }
}