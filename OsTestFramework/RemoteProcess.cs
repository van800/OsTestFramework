using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using JetBrains.OsTestFramework.Common;

namespace JetBrains.OsTestFramework
{
  /// <summary>
  /// A process running in the guest operating system.
  /// </summary>
  public class RemoteProcess
  {
    private readonly RemoteEnvironment _remoteEnvironment;
    public string GuestPId { get; private set; }

    /// <summary>
    /// A process running in the guest operating system on a virtual machine.
    /// </summary>
    /// <param name="remoteEnvironment">WindowsShell</param>
    /// <param name="guestPId"></param>
    public RemoteProcess(RemoteEnvironment remoteEnvironment, string guestPId)
    {
      _remoteEnvironment = remoteEnvironment;
      GuestPId = guestPId;
    }

    ///// <summary>
    ///// Kill the process tree in the guest operating system.
    ///// </summary>
    //public void KillProcessTreeInGuest(TimeSpan timeout)
    //{
    //  string command = string.Format("taskkill /PID {0} /T /F", GuestPId);
    //  _remoteEnvironment.WindowsShellInstance.ExecuteElevatedCommandInGuest(command, timeout);
    //  Thread.Sleep(TimeSpan.FromSeconds(1));
    //}

    /// <summary>
    /// Kill the process tree in the guest operating system.
    /// </summary>
    public void KillProcessTreeInGuest(TimeSpan timeout)
    {      
      try
      {
        ClientTaskKill(_remoteEnvironment.IpAddress, _remoteEnvironment.UserName, _remoteEnvironment.Password, GuestPId);
      }
      catch
      {
        string command = string.Format("taskkill /PID {0} /T /F", GuestPId);
        OsTestLogger.WriteLine("Direct taskkill failed, retrying with psexec");
        var so = _remoteEnvironment.WindowsShellInstance.ExecuteElevatedCommandInGuest(command, TimeSpan.FromSeconds(20));
        OsTestLogger.WriteLine("psexec-taskkill finished. Out: " + so.StdOut + ", err: " + so.StdErr);
        Thread.Sleep(1000);
      }
    }

    public IEnumerable<RemoteProcess> GetChildProcesses()
    {
      return _remoteEnvironment.WmiWrapperInstance.GetChildProcesses(_remoteEnvironment.WindowsShellInstance, this.GuestPId);
    }

    public static void ClientTaskKill(string ip, string userName, string password, string guestPId)
    {
      string args = string.Format("/S {0} /U {0}\\{1} /P {2} /PID {3} /T /F", ip, userName, password, guestPId);
      OsTestLogger.WriteLine("Executing at agent: taskkill " + args);
      string output = RunExternalExe("taskkill", args);
      OsTestLogger.WriteLine("taskkill output: " + output);
    }

    public override bool Equals(object obj)
    {
      return GuestPId.Equals(((RemoteProcess)obj).GuestPId);
    }

    public override int GetHashCode()
    {
      return GuestPId.GetHashCode();
    }

    public static string RunExternalExe(string filename, string arguments = null)
    {
      var process = new Process();

      process.StartInfo.FileName = filename;
      if (!string.IsNullOrEmpty(arguments))
      {
        process.StartInfo.Arguments = arguments;
      }

      process.StartInfo.CreateNoWindow = true;
      process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
      process.StartInfo.UseShellExecute = false;

      process.StartInfo.RedirectStandardError = true;
      process.StartInfo.RedirectStandardOutput = true;
      var stdOutput = new StringBuilder();
      process.OutputDataReceived += (sender, args) => stdOutput.Append(args.Data);

      string stdError = null;
      try
      {
        process.Start();
        process.BeginOutputReadLine();
        stdError = process.StandardError.ReadToEnd();
        process.WaitForExit();
      }
      catch (Exception e)
      {
        throw new Exception("OS error while executing " + Format(filename, arguments) + ": " + e.Message, e);
      }

      if (process.ExitCode == 0)
      {
        return stdOutput.ToString();
      }
      else
      {
        var message = new StringBuilder();

        if (!string.IsNullOrEmpty(stdError))
        {
          message.AppendLine(stdError);
        }

        if (stdOutput.Length != 0)
        {
          message.AppendLine("Std output:");
          message.AppendLine(stdOutput.ToString());
        }
        throw new Exception(Format(filename, arguments) + " finished with exit code = " + process.ExitCode + ": " + message);
      }
    }

    private static string Format(string filename, string arguments)
    {
      return "'" + filename + ((string.IsNullOrEmpty(arguments)) ? string.Empty : " " + arguments) + "'";
    }
  }
}