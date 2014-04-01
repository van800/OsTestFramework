using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using JetBrains.OsTestFramework.Common;
using JetBrains.OsTestFramework.Network;

namespace JetBrains.OsTestFramework
{
  public class WmiWrapper
  {
    private readonly string _ipAddress;
    private readonly string _userName;
    private readonly string _password;
    private ManagementScope _scope;

    public string ComputerName { get; private set; }
    public string WindowsDirectory { get; private set; }
    public string OperatingSystemString { get; private set; }
    public string Version { get; private set; }

    public WmiWrapper(string ip, string user, string password)
    {
      _ipAddress = ip;
      _userName = user;
      _password = password;

      //Query system for Operating System information
      var query = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
      var searcher = new ManagementObjectSearcher(GetScope(), query);

      ManagementObjectCollection queryCollection = searcher.Get();
      ComputerName = queryCollection.Cast<ManagementObject>().Select(w => w["csname"]).Single().ToString();
      WindowsDirectory = queryCollection.Cast<ManagementObject>().Select(w => w["WindowsDirectory"]).Single().ToString();
      OperatingSystemString = queryCollection.Cast<ManagementObject>().Select(w => w["Caption"]).Single().ToString();
      Version = queryCollection.Cast<ManagementObject>().Select(w => w["Version"]).Single().ToString();

      OsTestLogger.WriteLine(ComputerName);
      OsTestLogger.WriteLine(OperatingSystemString);
      OsTestLogger.WriteLine(Version);
    }

    public ManagementScope GetScope()
    {
      // create new scope every time, since on >500 tests at unpredictable time we got
      // The object invoked has disconnected from its clients. (Exception from HRESULT: 0x80010108 (RPC_E_DISCONNECTED))
      //if (_scope != null && _scope.IsConnected)
      //  return _scope;
      
      var options = new ConnectionOptions {Username = _ipAddress + @"\" + _userName, Password = _password};

      // Make a connection to a remote computer.
      // Replace the "FullComputerName" section of the
      // string "\\\\FullComputerName\\root\\cimv2" with
      // the full computer name or IP address of the
      // remote computer.
      _scope = new ManagementScope(string.Format(@"\\{0}\root\cimv2", _ipAddress), options);
      _scope.Connect();
      
      return _scope;
    }

    /// <summary>
    /// Tries to find the service. If no match - returns null
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public object TryGetServicePIdByNameInGuest(string name)
    {
      // Query services "PSEXESVC"
      var processSearcher = new ManagementObjectSearcher(GetScope(), new ObjectQuery(string.Format("SELECT ProcessId, Name FROM Win32_Service WHERE Name='{0}'", name)));
      var processQueryCollection = processSearcher.Get();
      var pid = processQueryCollection.Cast<ManagementObject>().Select(w => w["ProcessId"]).SingleOrDefault();
      OsTestLogger.WriteLine(string.Format("PId of the service {0}: {1}", name, pid));
      return pid;
    }

    /// <summary>
    /// Returns Ids and creation time of the direct childs
    /// </summary>
    /// <param name="windowsShell"></param>
    /// <param name="psExecPId"></param>
    /// <returns></returns>
    internal IEnumerable<RemoteProcess> GetChildProcesses(WindowsShell windowsShell, string psExecPId)
    {
      // Query processes
      var childSearcher = new ManagementObjectSearcher(GetScope(), new ObjectQuery("Select * From Win32_Process Where ParentProcessID=" + psExecPId));
      var childProcessQueryCollection = childSearcher.Get();
      //var childPid = childProcessQueryCollection.Cast<ManagementObject>().Select(w => w["ProcessId"]);
      //var CreationDate = childProcessQueryCollection.Cast<ManagementObject>().Select(w => w["CreationDate"]);
      IList<RemoteProcess> list = new List<RemoteProcess>();
      foreach (var process in childProcessQueryCollection)
      {
        string pid = process["ProcessId"].ToString();
        list.Add(new RemoteProcess(windowsShell.Env, pid));
      }
      return list;
    }
  }
}
