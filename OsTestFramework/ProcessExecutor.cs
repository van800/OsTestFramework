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
    private AutoResetEvent _outputWaitHandle;
    private Process _process;
    private readonly RemoteEnvironment _remoteEnvironment;

    public ProcessExecutor(RemoteEnvironment remoteEnvironment, string commandType, string psExecArg, string command,
      string[] args, TimeSpan startTimeout, TimeSpan? executionTimeout = null)
    {
      _executionTimeout = executionTimeout;
      _remoteEnvironment = remoteEnvironment;

      string arguments = GenerateArguments(psExecArg, command, args, startTimeout);

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

    public IElevatedCommandResult Execute()
    {
      _process.Start();

      _process.BeginOutputReadLine();
      _process.BeginErrorReadLine();

      int milliseconds = _executionTimeout.HasValue
        ? (int) _executionTimeout.Value.TotalMilliseconds
        : Int32.MaxValue;

      ElevatedCommandResult commandResult;
      if (_process.WaitForExit(milliseconds) && _outputWaitHandle.WaitOne(milliseconds) &&  _errorWaitHandle.WaitOne(milliseconds))
        commandResult =  new ElevatedCommandResult(_output.ToString(), _error.ToString(), _process.ExitCode, _process.ExitTime, _process.HasExited);
      else
        commandResult = new ElevatedCommandResult(_output.ToString(), _error.ToString());
      OsTestLogger.WriteLine(commandResult.ToString());
      return commandResult;
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

    private string GenerateArguments(string psExecArg, string command, string[] args, TimeSpan startTimeout)
    {
      string arg = "";
      if (args != null)
      {
        arg = args.Aggregate((a, b) => string.Format("{0} {1}", a, b));
      }

      string arguments = string.Format("-accepteula \\\\{0} -H -I{1} -N {2} -U {3} -P \"{4}\" {5} {6}",
        _remoteEnvironment.IpAddress, psExecArg,
        startTimeout.TotalSeconds, _remoteEnvironment.UserName, _remoteEnvironment.Password, command, arg);
      return arguments;
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
      }
      if (_outputWaitHandle != null)
      {
        _outputWaitHandle.Close();
        _outputWaitHandle = null;
      }
    }
  }
}