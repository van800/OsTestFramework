using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.OsTestFramework.Common;
using JetBrains.OsTestFramework.Network;

namespace JetBrains.OsTestFramework
{
  /// <summary>
  /// API root.
  /// </summary>
  public class RemoteEnvironment : IDisposable
  {
    public readonly string IpAddress;
    public readonly string UserName;
    public readonly string Password;
      private readonly string _psExecPath;
      public WmiWrapper WmiWrapperInstance { get; private set; }
    public WindowsShell WindowsShellInstance { get; set; }
    public Dictionary<string, string> GuestEnvironmentVariables { get; private set; }
    public bool IsUACEnabled { get; private set; }

      public string PsExecPath
      {
          get { return _psExecPath; }
      }

      public RemoteEnvironment(string ipAddress, string userName, string password, string psExecPath)
    {
      IpAddress = ipAddress;
      UserName = userName;
      Password = password;
        _psExecPath = psExecPath;

        // wait for ping
      RetryUtility.RetryAction(() =>
        {
          var ping = new Ping();
          var reply = ping.Send(IpAddress);
          OsTestLogger.WriteLine(string.Format("Pinging ip:{0}, reply: {1}", ipAddress, reply.Status));
          if (reply.Status != IPStatus.Success)
            throw new InvalidOperationException(string.Format("Remote IP {0} ping returns {1}", ipAddress, reply.Status));
        }, 50, 5000);

      // give time for the RPC service to start
      RetryUtility.RetryAction(() => { WmiWrapperInstance = new WmiWrapper(ipAddress, userName, password); }, 10, 5000);
      WindowsShellInstance = new WindowsShell(this, psExecPath);
      //this will populate the GuestEnvironmentVariables. Short time after start APPDATA may be empty
      RetryUtility.RetryAction( InvalidateCachedGuestEnvironmentVariables, 10, 10000);

      IsUACEnabled = CheckIsUAC();
    }

    /// <summary>
    /// Refills GuestEnvironmentVariables with current EnvVars from VM
    /// </summary>
    public void InvalidateCachedGuestEnvironmentVariables()
    {
      OsTestLogger.WriteLine(string.Format("Get environment variables"));
      GuestEnvironmentVariables = WindowsShellInstance.GetEnvironmentVariables();

      if (!GuestEnvironmentVariables.ContainsKey("APPDATA"))
        throw new Exception("APPDATA is not poputated. Be sure that the machine is really logged in on or retry several times, if the machine was shortly started.");
    }

    private void LogEnvironmentVariables()
    {
      var sb = new StringBuilder();
      var list = GuestEnvironmentVariables.Select(x => "[" + x.Key + " " + x.Value + "];");
      foreach (var item in list)
      {
        sb.Append(item);
      }
      OsTestLogger.WriteLine(sb.ToString());
    }

    public void CopyFileFromHostToGuest(string hostPath, string guestPath)
    {
      DoActionInGuest(guestPath, hostPath, (guestNetworkPath, fullHostPath) =>
          {
            OsTestLogger.WriteLine(string.Format(" Copying '{0}' => 'Remote:{1}'", fullHostPath, guestNetworkPath));
            FileOperations.CopyFiles(fullHostPath, guestNetworkPath);
          });
    }

    public void CopyFileFromGuestToHost(string guestPath, string hostPath)
    {
      DoActionInGuest(guestPath, hostPath,
        (guestNetworkPath, fullHostPath) =>
          {
            OsTestLogger.WriteLine(string.Format(" Copying 'Remote:{0}' => '{1}'", guestNetworkPath, fullHostPath));
            FileOperations.CopyFiles(guestNetworkPath, fullHostPath);
          });
    }

    private void DoActionInGuest(string guestPath, string hostPath, Action<string, string> action)
    {
      string fullHostPath = Path.GetFullPath(hostPath);
      string fullGuestPath = Path.GetFullPath(guestPath);
      string guestRootPath = Path.GetPathRoot(fullGuestPath);
      var mappedNetworkDriveInfo = new MappedNetworkDriveInfo(guestRootPath);
      mappedNetworkDriveInfo.Username = UserName;
      mappedNetworkDriveInfo.Password = Password;
      mappedNetworkDriveInfo.Auto = false;
      OsTestLogger.WriteLine(string.Format(" Mapping 'Remote:{0}' as '{1}'", mappedNetworkDriveInfo.RemotePath, UserName));
      using (var mappedNetworkDrive = new MappedNetworkDrive(this.IpAddress, mappedNetworkDriveInfo))
      {
        string guestNetworkPath = mappedNetworkDrive.GuestPathToNetworkPath(fullGuestPath);
        string guestNetworkRootPath = mappedNetworkDrive.GuestPathToNetworkPath(guestRootPath);
        OsTestLogger.WriteLine(string.Format(" Resolving 'Remote:{0}'", guestNetworkRootPath));
        mappedNetworkDrive.MapNetworkDrive();
        action(guestNetworkPath, fullHostPath);
      }
    }

    public bool RemoveReadOnlyAttributeFromFile(string guestFilePath)
    {
        return DoActionInGuest(guestFilePath, (guestNetworkPath) =>
        {
          OsTestLogger.WriteLine(string.Format(" File.RemoveReadOnly? 'Remote:{0}'", guestNetworkPath));
            var fileInfo = new FileInfo(guestFilePath) {IsReadOnly = false};
            fileInfo.Refresh();
            return true;
        });
    }

    public bool RemoveReadOnlyAttributeFromDirectory(string guestDirPath)
    {
        return DoActionInGuest(guestDirPath, (guestNetworkPath) =>
        {
          OsTestLogger.WriteLine(string.Format(" Directory.RemoveReadOnly? 'Remote:{0}'", guestNetworkPath));
            var directoryInfo = new DirectoryInfo(guestDirPath);
            var fileInfos = directoryInfo.GetFileSystemInfos();
            foreach (var fileSystemInfo in fileInfos)
            {
                var modifiedAttributes = RemoveAttribute(fileSystemInfo.Attributes, FileAttributes.ReadOnly);
                File.SetAttributes(fileSystemInfo.FullName, modifiedAttributes);
            }
            return true;
        });
    }

    private static FileAttributes RemoveAttribute(FileAttributes attributes, FileAttributes attributesToRemove)
    {
        return attributes & ~attributesToRemove;
    }

    public bool FileExistsInGuest(string guestPath)
    {
      return DoActionInGuest(guestPath, (guestNetworkPath) =>
        {
          OsTestLogger.WriteLine(string.Format(" File.Exists? 'Remote:{0}'", guestNetworkPath));
          return File.Exists(guestNetworkPath);
        });
    }

    public bool DirectoryExistsInGuest(string guestPath)
    {
      return DoActionInGuest(guestPath, (guestNetworkPath) =>
        {
          OsTestLogger.WriteLine(string.Format(" Directory.Exists? 'Remote:{0}'", guestNetworkPath));
          return Directory.Exists(guestNetworkPath);
        });
    }

    public void DeleteDirectoryFromGuest(string guestPath)
    {
      DoActionInGuest(guestPath, (guestNetworkPath) =>
        {
          OsTestLogger.WriteLine(string.Format(" Directory.Delete 'Remote:{0}'", guestNetworkPath));
          Directory.Delete(guestNetworkPath);
          return true;
        });
    }

    public void DeleteDirectoryFromGuest(string guestPath, bool recursive)
    {
      DoActionInGuest(guestPath, (guestNetworkPath) =>
        {
          OsTestLogger.WriteLine(string.Format(" Directory.Delete 'Remote:{0}'", guestNetworkPath));
          Directory.Delete(guestNetworkPath, recursive);
          return true;
        });
    }

    public void DeleteFileFromGuest(string guestPath)
    {
      DoActionInGuest(guestPath, (guestNetworkPath) =>
          {
            OsTestLogger.WriteLine(string.Format(" File.Delete 'Remote:{0}'", guestNetworkPath));
            File.Delete(guestNetworkPath);
            return true;
          });
    }

    public void DeleteFileFromGuest(string guestPath, bool ignoreExceptions)
    {
      DoActionInGuest(guestPath, (guestNetworkPath) =>
          {
            OsTestLogger.WriteLine(string.Format(" File.Delete 'Remote:{0}'", guestNetworkPath));
              try
              {
                  File.Delete(guestNetworkPath);
              }
              catch (Exception)
              {
                  if (!ignoreExceptions) throw;
              }
              return true;
          });
    }
    
    public void CreateDirectoryInGuest(string guestPath)
    {
      DoActionInGuest(guestPath, (guestNetworkPath) =>
      {
        OsTestLogger.WriteLine(string.Format(" Directory.CreateDirectory 'Remote:{0}'", guestNetworkPath));
        Directory.CreateDirectory(guestNetworkPath);
        return true;
      });
    }

    private bool DoActionInGuest(string guestPath, Func<string,bool> action)
    {
      string fullGuestPath = Path.GetFullPath(guestPath);
      string guestRootPath = Path.GetPathRoot(fullGuestPath);
      var mappedNetworkDriveInfo = new MappedNetworkDriveInfo(guestRootPath);
      mappedNetworkDriveInfo.Username = UserName;
      mappedNetworkDriveInfo.Password = Password;
      mappedNetworkDriveInfo.Auto = false;
      OsTestLogger.WriteLine(string.Format(" Mapping 'Remote:{0}' as '{1}'", mappedNetworkDriveInfo.RemotePath, UserName));
      using (var mappedNetworkDrive = new MappedNetworkDrive(this.IpAddress, mappedNetworkDriveInfo))
      {
        string guestNetworkPath = mappedNetworkDrive.GuestPathToNetworkPath(fullGuestPath);
        string guestNetworkRootPath = mappedNetworkDrive.GuestPathToNetworkPath(guestRootPath);
        OsTestLogger.WriteLine(string.Format(" Resolving 'Remote:{0}'", guestNetworkRootPath));
        mappedNetworkDrive.MapNetworkDrive();
        return action(guestNetworkPath);
      }
    }
    
    private bool CheckIsUAC()
    {
      if (WmiWrapperInstance.Version.StartsWith("5")) //windows xp
      {
        OsTestLogger.WriteLine("Windows xp. UAC is always off.");
        return false;
      }
     
      try
      {
        string uace = ReadRegistryValue32(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA");
        bool result = uace.Contains('1');
        OsTestLogger.WriteLine("Guest machine UAC is " + (result ? "enabled" : "disabled") + ". ReadRegistryValue32 was successful.");
        return result;
      }
      catch (Exception e)
      {
        OsTestLogger.WriteLine("ReadRegistryValue32 throws exception. Consider UAC is enabled. e = "+ e);
        return true;
      }
    }

    public string ReadRegistryValue32(string keyName, string valueName)
    {
      var regPath64 = @"C:\Windows\SysWOW64\reg.exe";
      var regPath32 = @"C:\Windows\system32\reg.exe";
      var regPath = FileExistsInGuest(regPath64) ? regPath64 : regPath32;
      string line = string.Format("{0} QUERY \"{1}\" /v \"{2}\"", regPath, keyName, valueName);
      OsTestLogger.WriteLine(string.Format("ReadRegistryValue with {0} : {1}", regPath, line));
      var output = WindowsShellInstance.ExecuteElevatedCommandInGuest(line, TimeSpan.FromSeconds(60));
      return ParseRegValue(output.StdOut, valueName);
    }

    private static string ParseRegValue(string stdOut, string lineStart)
    {
      using (var reader = new StringReader(stdOut))
      {
        string line;
        while ((line = reader.ReadLine()) != null)
        {
          line = line.TrimStart(" ".ToCharArray());
          if (line.StartsWith(lineStart))
          {
            line = line.Substring(lineStart.Length, line.Length - lineStart.Length);
            line = line.TrimStart(" ".ToCharArray());
            var regex = new Regex(@"^\s*REG_\w+\s+(?<Value>.+)$");
            var match = regex.Match(line);
            if (match.Success)
              return match.Groups["Value"].Value;
          }
        }
      }
      throw new Exception("ReadRegistryKey failed. stdOut:" + stdOut);
    }

    /// <summary>
    /// Creates temp file locally and moves it to C:\TMP1\ location in guest. Returns guest path.
    /// </summary>
    /// <returns></returns>
    public string CreateTempFileInGuest()
    {
      var tempFile = Path.GetTempFileName();
      string guestPath = Path.Combine(@"C:\TMP1\", Path.GetFileName(tempFile));
      CopyFileFromHostToGuest(tempFile, guestPath);
      File.Delete(tempFile);
      return guestPath;
    }

    public void Dispose()
    {
      if (WindowsShellInstance!=null)
        WindowsShellInstance.Dispose();
    }
  }
}
