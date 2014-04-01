using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading;
using NUnit.Framework;

namespace JetBrains.OsTestFramework.Test
{
  [TestFixture]
  public class RemoteEnvironmentTest
  {
    private readonly string _assemblyDirectory = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).AbsolutePath);

    [Test]
    public void InvalidateCachedGuestEnvironmentVariablesTest()
    {
      string ip = "172.20.241.223";
      string userName = "user";
      string password = "123";

      using (var operatingSystem = new RemoteEnvironment(ip, userName, password, Path.Combine(_assemblyDirectory, @"..\tools\PsExec.exe")))
      {
        operatingSystem.InvalidateCachedGuestEnvironmentVariables();
        Console.WriteLine(operatingSystem.GuestEnvironmentVariables.Count);  
      }
      
    }

    [Test]
    public void DisposeTest()
    {
      string ip = "172.20.241.223";
      string userName = "user";
      string password = "123";

      using (var operatingSystem = new RemoteEnvironment(ip, userName, password, Path.Combine(_assemblyDirectory, @"..\tools\PsExec.exe")))
      {
        operatingSystem.WindowsShellInstance.ExecuteElevatedCommandInGuestNoRemoteOutput(@"cmd.exe /c", TimeSpan.FromSeconds(10));
      }
    }

    [Test]
    public void CopyFileTest()
    {
      string ip = "172.20.240.79";
      string userName = "LABS\builuser";
      string password = "***REMOVED***";

      var operatingSystem = new RemoteEnvironment(ip, userName, password, Path.Combine(_assemblyDirectory, @"..\tools\PsExec.exe"));
      operatingSystem.CopyFileFromGuestToHost(@"C:\Downloads\7z920-x64.msi", @"C:\Temp");
      //Console.WriteLine(operatingSystem.GuestEnvironmentVariables.Count);
    }

    [Test]
    public void ProcessInfoTest1()
    {
      string ip = "192.168.75.128";
      string userName = "Administrator";
      string password = "123";

      var operatingSystem = new RemoteEnvironment(ip, userName, password, Path.Combine(_assemblyDirectory, @"..\tools\PsExec.exe"));

      operatingSystem.WindowsShellInstance.DetachElevatedCommandInGuestNoRemoteOutput(@"C:\Ttte\OsTestFramework.TakeScreenshot.exe C:\Ttte\1.jpg", TimeSpan.FromSeconds(10));
      
    }
    
    [Test]
    public void ProcessInfoTest()
    {
      //var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
      //ManagementObjectCollection moc = searcher.Get();
      //foreach (ManagementObject mo in moc)
      //{
      //  KillProcessAndChildren(Convert.ToInt32((object)mo["ProcessID"]));
      //}

      /*// Build an options object for the remote connection
        //   if you plan to connect to the remote
        //   computer with a different user name
        //   and password than the one you are currently using
          
             ConnectionOptions options = 
                 new ConnectionOptions();
                 
             // and then set the options.Username and 
             // options.Password properties to the correct values
             // and also set 
             // options.Authority = "ntdlmdomain:DOMAIN";
             // and replace DOMAIN with the remote computer's
             // domain.  You can also use kerberose instead
             // of ntdlmdomain.
        */
      string ip = "192.168.75.128";
      string userName = "Administrator";
      string password = "123";
      var options = new ConnectionOptions { Username = ip + @"\" + userName, Password = password };

      // Make a connection to a remote computer.
      // Replace the "FullComputerName" section of the
      // string "\\\\FullComputerName\\root\\cimv2" with
      // the full computer name or IP address of the
      // remote computer.
      var scope = new ManagementScope(string.Format(@"\\{0}\root\cimv2", ip), options);
      scope.Connect();

      // Query services
      var processSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(string.Format("SELECT ProcessId, Name FROM Win32_Service WHERE Name='{0}'", "PSEXESVC")));
      var processQueryCollection = processSearcher.Get();
      var pid = processQueryCollection.Cast<ManagementObject>().Select(w => w["ProcessId"]).Single();
      Console.WriteLine("Pid : {0}", pid);

      // Query processes
      var childSearcher = new ManagementObjectSearcher(scope, new ObjectQuery("Select * From Win32_Process Where ParentProcessID=" + pid));
      var childProcessQueryCollection = childSearcher.Get();
      var childPid = childProcessQueryCollection.Cast<ManagementObject>().Select(w => w["ProcessId"]).Single();
      var CreationDate = childProcessQueryCollection.Cast<ManagementObject>().Select(w => w["CreationDate"]).Single();

      Console.WriteLine("childPid : {0}", childPid);
      Console.WriteLine("CreationDate : {0}", CreationDate);


      /* TextFile.WriteLine "Caption: " & SubItems.Caption
    TextFile.WriteLine "CommandLine: " & SubItems.CommandLine
    TextFile.WriteLine "CreationClassName: " & SubItems.CreationClassName
    TextFile.WriteLine "CreationDate: " & SubItems.CreationDate
    TextFile.WriteLine "CSCreationClassName: " & SubItems.CSCreationClassName
    TextFile.WriteLine "CSName: " & SubItems.CSName
    TextFile.WriteLine "Description: " & SubItems.Description
    TextFile.WriteLine "ExecutablePath: " & SubItems.ExecutablePath
    TextFile.WriteLine "ExecutionState: " & SubItems.ExecutionState
    TextFile.WriteLine "Handle: " & SubItems.Handle
    TextFile.WriteLine "HandleCount: " & SubItems.HandleCount
    TextFile.WriteLine "InstallDate: " & SubItems.InstallDate
    TextFile.WriteLine "KernelModeTime: " & SubItems.KernelModeTime
    TextFile.WriteLine "MaximumWorkingSetSize: " & SubItems.MaximumWorkingSetSize
    TextFile.WriteLine "MinimumWorkingSetSize: " & SubItems.MinimumWorkingSetSize
    TextFile.WriteLine "Name: " & SubItems.Name
    TextFile.WriteLine "OSCreationClassName: " & SubItems.OSCreationClassName
    TextFile.WriteLine "OSName: " & SubItems.OSName
    TextFile.WriteLine "OtherOperationCount: " & SubItems.OtherOperationCount
    TextFile.WriteLine "OtherTransferCount: " & SubItems.OtherTransferCount
    TextFile.WriteLine "PageFaults: " & SubItems.PageFaults
    TextFile.WriteLine "PageFileUsage: " & SubItems.PageFileUsage
    TextFile.WriteLine "ParentProcessId: " & SubItems.ParentProcessId
    TextFile.WriteLine "PeakPageFileUsage: " & SubItems.PeakPageFileUsage
    TextFile.WriteLine "PeakVirtualSize: " & SubItems.PeakVirtualSize
    TextFile.WriteLine "PeakWorkingSetSize: " & SubItems.PeakWorkingSetSize
    TextFile.WriteLine "Priority: " & SubItems.Priority
    TextFile.WriteLine "PrivatePageCount: " & SubItems.PrivatePageCount
    TextFile.WriteLine "ProcessId: " & SubItems.ProcessId
    TextFile.WriteLine "QuotaNonPagedPoolUsage: " & SubItems.QuotaNonPagedPoolUsage
    TextFile.WriteLine "QuotaPagedPoolUsage: " & SubItems.QuotaPagedPoolUsage
    TextFile.WriteLine "QuotaPeakNonPagedPoolUsage: " & SubItems.QuotaPeakNonPagedPoolUsage
    TextFile.WriteLine "QuotaPeakPagedPoolUsage: " & SubItems.QuotaPeakPagedPoolUsage
    TextFile.WriteLine "ReadOperationCount: " & SubItems.ReadOperationCount
    TextFile.WriteLine "ReadTransferCount: " & SubItems.ReadTransferCount
    TextFile.WriteLine "SessionId: " & SubItems.SessionId
    TextFile.WriteLine "Status: " & SubItems.Status
    TextFile.WriteLine "TerminationDate: " & SubItems.TerminationDate
    TextFile.WriteLine "ThreadCount: " & SubItems.ThreadCount
    TextFile.WriteLine "UserModeTime: " & SubItems.UserModeTime
    TextFile.WriteLine "VirtualSize: " & SubItems.VirtualSize
    TextFile.WriteLine "WindowsVersion: " & SubItems.WindowsVersion
    TextFile.WriteLine "WorkingSetSize: " & SubItems.WorkingSetSize
    TextFile.WriteLine "WriteOperationCount: " & SubItems.WriteOperationCount
    TextFile.WriteLine "WriteTransferCount: " & SubItems.WriteTransferCount
       */
    }
  }
}
