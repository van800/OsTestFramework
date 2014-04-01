﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using JetBrains.OsTestFramework.Common;

namespace JetBrains.OsTestFramework.Network
{
  /// <summary>
  /// A shell wrapper capable of executing remote commands on Microsoft Windows.
  /// </summary>
  public class WindowsShell : IDisposable
  {
    private PsExecWrapper PsExecWrapperInstance;
    /// <summary>
    /// New instance of a guest operating system wrapper.
    /// </summary>
    /// <param name="env">Remote environment.</param>
    public WindowsShell(RemoteEnvironment env, string psExecPath)
    {
      Env = env;
      PsExecWrapperInstance = new PsExecWrapper(psExecPath, env);

      // Warmup run of cmd using psexec. Required mainly for the case of machines with some network problem on startup
      OsTestLogger.WriteLine("Start sample initial program with big timeout to ensure all remaining will be also fine with short timeout.");
      RetryUtility.RetryAction(
        () => DetachElevatedCommandInGuestNoRemoteOutput("cmd.exe /c echo \"cmd was run ok.\"", TimeSpan.FromSeconds(180)), 50, 2000);
    }

    public RemoteEnvironment Env { get; private set; }

    /// <summary>
    /// Read a file in the guest operating system.
    /// </summary>
    /// <param name="guestFilename">File in the guest operating system.</param>
    /// <returns>File contents as a string.</returns>
    public string ReadFile(string guestFilename)
    {
      return ReadFile(guestFilename, Encoding.Default);
    }

    /// <summary>
    /// Read a file in the guest operating system.
    /// </summary>
    /// <param name="guestFilename">File in the guest operating system.</param>
    /// <param name="encoding">Encoding applied to the file contents.</param>
    /// <returns>File contents as a string.</returns>
    public string ReadFile(string guestFilename, Encoding encoding)
    {
      string tempFilename = Path.GetTempFileName();
      try
      {
        Env.CopyFileFromGuestToHost(guestFilename, tempFilename);
        return File.ReadAllText(tempFilename, encoding);
      }
      finally
      {
        File.Delete(tempFilename);
      }
    }

    /// <summary>
    /// Read a file in the guest operating system.
    /// </summary>
    /// <param name="guestFilename">File in the guest operating system.</param>
    /// <returns>File contents as bytes.</returns>
    public byte[] ReadFileBytes(string guestFilename)
    {
      string tempFilename = Path.GetTempFileName();
      try
      {
        Env.CopyFileFromGuestToHost(guestFilename, tempFilename);
        return File.ReadAllBytes(tempFilename);
      }
      finally
      {
        File.Delete(tempFilename);
      }
    }

    /// <summary>
    /// Read a file in the guest operating system.
    /// </summary>
    /// <param name="guestFilename">File in the guest operating system.</param>
    /// <returns>File contents, line-by-line.</returns>
    public string[] ReadFileLines(string guestFilename)
    {
      return ReadFileLines(guestFilename, Encoding.Default);
    }

    /// <summary>
    /// Read a file in the guest operating system.
    /// </summary>
    /// <param name="guestFilename">File in the guest operating system.</param>
    /// <param name="encoding">Encoding applied to the file contents.</param>
    /// <returns>File contents, line-by-line.</returns>
    public string[] ReadFileLines(string guestFilename, Encoding encoding)
    {
      string tempFilename = Path.GetTempFileName();
      try
      {
        Env.CopyFileFromGuestToHost(guestFilename, tempFilename);
        return File.ReadAllLines(tempFilename, encoding);
      }
      finally
      {
        File.Delete(tempFilename);
      }
    }

    /// <summary>
    /// WindowsShell output.
    /// </summary>
    public struct ShellOutput
    {
      /// <summary>
      /// Standard output.
      /// </summary>
      public string StdOut;

      /// <summary>
      /// Standard error.
      /// </summary>
      public string StdErr;
    }

    /// <summary>
    /// Executes cmd.exe /C "guestCommandLine" > file and parses the result.
    /// </summary>
    /// <param name="guestCommandLine">Guest command line, argument passed to cmd.exe.</param>
    /// <param name="startTimeout"></param>
    /// <returns>Standard output.</returns>
    public ShellOutput ExecuteElevatedCommandInGuest(string guestCommandLine, TimeSpan startTimeout)
    {
      string guestStdOutFilename = Env.CreateTempFileInGuest();
      string guestStdErrFilename = Env.CreateTempFileInGuest();
      string guestCommandBatch = Env.CreateTempFileInGuest() + ".bat";
      string hostCommandBatch = Path.GetTempFileName();
      var hostCommand = new StringBuilder();
      hostCommand.AppendLine("@echo off");
      hostCommand.AppendLine(guestCommandLine);
      File.WriteAllText(hostCommandBatch, hostCommand.ToString());
      try
      {
        Env.CopyFileFromHostToGuest(hostCommandBatch, guestCommandBatch);
        string cmdArgs = string.Format("> \"{0}\" 2>\"{1}\"", guestStdOutFilename, guestStdErrFilename);
        OsTestLogger.WriteLine("ExecuteElevatedCommandInGuest: " + guestCommandLine);
        PsExecWrapperInstance.ExecuteElevatedCommandInGuest(guestCommandBatch + " " + cmdArgs, null, startTimeout);
        
        var output = new ShellOutput();
        output.StdOut = ReadFile(guestStdOutFilename);
        output.StdErr = ReadFile(guestStdErrFilename);

        OsTestLogger.WriteLine("VM_StdOut:" + output.StdOut);
        OsTestLogger.WriteLine("VM_StdErr:" + output.StdErr);

        return output;
      }
      finally
      {
        File.Delete(hostCommandBatch);
        Env.DeleteFileFromGuest(guestCommandBatch);
        Env.DeleteFileFromGuest(guestStdOutFilename);
        Env.DeleteFileFromGuest(guestStdErrFilename);
      }
    }

    /// <summary>
    /// Run command and wait till process ends. Doesn't provide remote output.
    /// Plus: directly runs the command - console is not started in VM
    /// Minus: you will not get any output of the executed command
    /// If you need the output - use WindowsShell.ExecuteElevatedCommandInGuest
    /// </summary>
    /// <param name="guestCommandLine"></param>
    /// <param name="startTimeout"></param>
    public void ExecuteElevatedCommandInGuestNoRemoteOutput(string guestCommandLine, TimeSpan startTimeout)
    {
      PsExecWrapperInstance.ExecuteElevatedCommandInGuest(guestCommandLine, null, startTimeout);
    }

    /// <summary>
    /// Detaches command and immediatelly returns.
    /// Plus: directly runs the command - console is not started in VM
    /// Minus: you will not get any output of the executed command
    /// If you need the output - use WindowsShell.ExecuteElevatedCommandInGuest
    /// </summary>
    /// <param name="guestCommandLine"></param>
    /// <param name="startTimeout"></param>
    public RemoteProcess DetachElevatedCommandInGuestNoRemoteOutput(string guestCommandLine, TimeSpan startTimeout)
    {
      var tick = DateTime.Now;
      Match m = Regex.Match("fail", @"with process ID \d+.");

      while (!m.Success && (DateTime.Now - tick) <= startTimeout)
      {
        var line = PsExecWrapperInstance.DetachElevatedCommandInGuest(guestCommandLine, null, startTimeout);
        m = Regex.Match(line, @"with process ID \d+.");
        Thread.Sleep(2000);
      }

      if (!m.Success)
        throw new InvalidOperationException("PsExec failed to start command: "+ guestCommandLine);

      Match m2 = Regex.Match(m.Value, @"\d+");
      OsTestLogger.WriteLine("Process ID="+m2.Value);
      int pid = Convert.ToInt32(m2.Value);
      return new RemoteProcess(Env, pid.ToString());
    }

    /// <summary>
    /// Use BeginElevatedCommandInGuest to execute cmd.exe /C "guestCommandLine" > file and return process.
    /// </summary>
    /// <param name="guestCommandLine">Guest command line, argument passed to cmd.exe.</param>
    [Obsolete("Switch to DetachElevatedCommandInGuestNoRemoteOutput")]
    public RemoteProcess BeginElevatedCommandInGuest(string guestCommandLine, TimeSpan startTimeout)
    {
      string guestCommandBatch = Env.CreateTempFileInGuest() + ".bat";
      string hostCommandBatch = Path.GetTempFileName();
      File.WriteAllText(hostCommandBatch, guestCommandLine);
      try
      {
        Env.CopyFileFromHostToGuest(hostCommandBatch, guestCommandBatch);
        OsTestLogger.WriteLine("BeginElevatedCommandInGuest: " + guestCommandLine);
        return PsExecWrapperInstance.BeginElevatedCommandInGuest(guestCommandBatch, startTimeout);
      }
      finally
      {
        File.Delete(hostCommandBatch);
        // Could not be deleted while executing
        //_vm.DeleteFileFromGuest(guestCommandBatch); 
      }
    }

    private static StringBuilder PrepareEnvVars(Dictionary<string, string> variables)
    {
      var sb = new StringBuilder();
      if (variables != null)
      {
        foreach (var variable in variables)
        {
          string line = string.Format("SET {0}={1}", variable.Key, variable.Value);
          sb.Append(line);
          sb.Append(Environment.NewLine);
        }
      }
      return sb;
    }

    public void SetGlobalEnvVar(string key, string value)
    {
      OsTestLogger.WriteLine("SetGlobalEnvVar");
      var output = ExecuteElevatedCommandInGuest(string.Format("setx {0} {1} -m", key, value), TimeSpan.FromSeconds(60));
      OsTestLogger.WriteLine(output.StdOut);
      OsTestLogger.WriteLine(output.StdErr);
    }

    public void SetUserEnvVar(string key, string value)
    {
      OsTestLogger.WriteLine("SetUserEnvVar");
      var output = ExecuteElevatedCommandInGuest(string.Format("setx {0} {1}", key, value), TimeSpan.FromSeconds(60));
      OsTestLogger.WriteLine(output.StdOut);
      OsTestLogger.WriteLine(output.StdErr);
    }

    /// <summary>
    /// Returns environment variables parsed from the output of a set command.
    /// </summary>
    /// <returns>Environment variables.</returns>
    /// <example>
    /// <para>
    /// The following example retrieves the ProgramFiles environment variable from the guest operating system.
    /// <code language="cs" source="..\Source\VMWareToolsSamples\WindowsShellSamples.cs" region="Example: Enumerating Environment Variables on the GuestOS without VixCOM" />
    /// </para>
    /// </example>
    public Dictionary<string, string> GetEnvironmentVariables()
    {
      var environmentVariables = new Dictionary<string, string>();
      var sr = new StringReader(ExecuteElevatedCommandInGuest("set", TimeSpan.FromSeconds(60)).StdOut);
      string line = null;
      while (!string.IsNullOrEmpty(line = sr.ReadLine()))
      {
        string[] nameValuePair = line.Split("=".ToCharArray(), 2);
        if (nameValuePair.Length != 2)
        {
          throw new Exception(string.Format("Invalid environment string: \"{0}\"", line));
        }

        environmentVariables[nameValuePair[0]] = nameValuePair[1];
      }
      return environmentVariables;
    }

    public void Dispose()
    {
      var service = Env.WmiWrapperInstance.TryGetServicePIdByNameInGuest("PSEXESVC");
      if (service != null)
      {
        RemoteProcess.ClientTaskKill(Env.IpAddress, Env.UserName, Env.Password, service.ToString());
        Thread.Sleep(1000);
      }
    }
  }
}