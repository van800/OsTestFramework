using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using JetBrains.OsTestFramework.Common;

namespace JetBrains.OsTestFramework
{
  class PsExecWrapper
  {
    public readonly string PsExecPath;
    private readonly RemoteEnvironment _remoteEnvironment;

    public PsExecWrapper(string psExecPath, RemoteEnvironment remoteEnvironment)
    {
      PsExecPath = psExecPath;
      _remoteEnvironment = remoteEnvironment;
    }

    private static StringBuilder _output;

    /// <summary>
    /// Returns host psexec Output+ErrOutPut
    /// </summary>
    /// <param name="command"></param>
    /// <param name="args"></param>
    /// <param name="startTimeout"></param>
    /// <returns></returns>
    public string ExecuteElevatedCommandInGuest(string command, string[] args, TimeSpan startTimeout)
    {
      return ElevatedCommandInGuest("Execute", "", command, args, startTimeout);
    }

    public string DetachElevatedCommandInGuest(string command, string[] args, TimeSpan startTimeout)
    {
      return ElevatedCommandInGuest("Detach", " -D", command, args, startTimeout);
    }

    private string ElevatedCommandInGuest(string commandType, string psExecArg, string command, string[] args, TimeSpan startTimeout)
    {
      _output = new StringBuilder();
      string arg = "";
      if (args != null)
        arg = args.Aggregate((a, b) => string.Format("{0} {1}", a, b));
      var psi = new ProcessStartInfo(PsExecPath, string.Format("-accepteula \\\\{0} -H -I{1} -N {2} -U {3} -P \"{4}\" {5} {6}", _remoteEnvironment.IpAddress, psExecArg,
          startTimeout.TotalSeconds, _remoteEnvironment.UserName, _remoteEnvironment.Password, command, arg));
      psi.UseShellExecute = false;
      psi.ErrorDialog = false;
      psi.RedirectStandardOutput = true;
      psi.RedirectStandardError = true; // DO NOT REMOVE. Adding this will avoid getting some error output of psexec in nunit-console
      psi.CreateNoWindow = true;
      OsTestLogger.WriteLine("Running " + commandType + "ElevatedCommandInGuest: " + psi.FileName + " " + psi.Arguments);
      var process = Process.Start(psi);
      process.BeginOutputReadLine();
      process.OutputDataReceived += retval_OutputDataReceived;
      var stdError = process.StandardError.ReadToEnd();
      process.WaitForExit();
      if (!process.HasExited)
      {
        process.Kill();
      }
      var result = ("StdErr:" + stdError + "StdOut:" + _output).Replace("\r", " ").Replace("\n", " ");
      OsTestLogger.WriteLine(result);
      return result;
    }

    /// <summary>
    /// Use ExecuteElevatedCommandInGuest to execute cmd.exe /C "guestCommandLine" > file and return process.
    /// </summary>
    /// <param name="guestCommandLine">Guest command line, argument passed to cmd.exe.</param>
    /// <param name="startTimeout"></param>
    [Obsolete("Switch to DetachElevatedCommandInGuest")]
    public RemoteProcess BeginElevatedCommandInGuest(string guestCommandLine, TimeSpan startTimeout)
    {
      var start = DateTime.Now;
      _output = new StringBuilder();
      //"psexec.exe \\REMOTECOMPUTER –i –u username –p Password –d yourexe.exe"
      var psi = new ProcessStartInfo(PsExecPath, string.Format("-accepteula \\\\{0} -H -I -U {1} -P \"{2}\" {3}", _remoteEnvironment.IpAddress, _remoteEnvironment.UserName, _remoteEnvironment.Password, guestCommandLine))
        {
          UseShellExecute = false,
          ErrorDialog = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true
        };
      
      // if before starting process the PSEXESVC doesn't exist - the only child would be the target process
      // if PSEXESVC exists - check the clilds before starting and wait for the new process
      var psExecPId0 = _remoteEnvironment.WmiWrapperInstance.TryGetServicePIdByNameInGuest("PSEXESVC");
      
      var childs0 = new List<RemoteProcess>();
      if (psExecPId0 != null)
      {
        childs0.AddRange(_remoteEnvironment.WmiWrapperInstance.GetChildProcesses(_remoteEnvironment.WindowsShellInstance, psExecPId0.ToString()));  
      }
      
      OsTestLogger.WriteLine("Start Process: " + psi.FileName + " " + psi.Arguments);
      var localProcess = Process.Start(psi);  
      
      // wait for the new process after running the new one
      RemoteProcess process = null;
      
      int i = 0;
      while (process==null)
      {
        OsTestLogger.WriteLine("Interation: " + i);
        var psExecPId = _remoteEnvironment.WmiWrapperInstance.TryGetServicePIdByNameInGuest("PSEXESVC");
        if (psExecPId!=null && psExecPId.ToString()!="" && psExecPId.ToString()!="0")
        {
          Thread.Sleep(1000);
          if (psExecPId0 == null) // new psexec
          {
            OsTestLogger.WriteLine("New PsExec started. Expecting single child process.");
            var possibleProcesses = _remoteEnvironment.WmiWrapperInstance.GetChildProcesses(_remoteEnvironment.WindowsShellInstance, psExecPId.ToString()).ToList();
            foreach (var possibleProcess in possibleProcesses)
            {
              OsTestLogger.WriteLine("possibleProcess: " + possibleProcess.GuestPId);
            }
            process = possibleProcesses.SingleOrDefault();  
          }
          else // psexec was already running
          {
            OsTestLogger.WriteLine("PsExec already present with the following child processes:");
            foreach (var remoteProcess in childs0)
            {
              OsTestLogger.WriteLine("PId:" + remoteProcess.GuestPId);  
            }

            process = _remoteEnvironment.WmiWrapperInstance.GetChildProcesses(_remoteEnvironment.WindowsShellInstance, psExecPId.ToString()).SingleOrDefault(a => !childs0.Contains(a));
          }
        }
        Thread.Sleep(1000);
        if ((DateTime.Now - start).Duration() > startTimeout)
        {
          localProcess.Kill();
          throw new Exception(string.Format("Guest process {0} {1} was not started in {2} seconds.", psi.FileName, psi.Arguments, startTimeout.TotalSeconds));
        }
        i++;
      }

      return process;
    }

    void retval_OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
      if (e != null && e.Data != null)
      {
        //OsTestLogger.WriteLine("e.Data="+e.Data);
        _output.Append(e.Data);
      }
    }
  }
}
