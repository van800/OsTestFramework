﻿using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using JetBrains.OsTestFramework.Common;

namespace JetBrains.OsTestFramework.Network
{
  /// <summary>
  /// Contains constructor information for a mapped network drive.
  /// </summary>
  public class MappedNetworkDriveInfo
  {
    /// <summary>
    /// Remote network path.
    /// </summary>
    public string RemotePath;

    /// <summary>
    /// Local network path.
    /// </summary>
    public string LocalPath;

    /// <summary>
    /// Optional network username.
    /// </summary>
    public string Username;

    /// <summary>
    /// Optional network password.
    /// </summary>
    public string Password;

    /// <summary>
    /// Automatically map network drive.
    /// </summary>
    public bool Auto = true;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public MappedNetworkDriveInfo()
    {
    }

    /// <summary>
    /// Default constructor for a remote path.
    /// </summary>
    /// <param name="remotePath">Remote path.</param>
    public MappedNetworkDriveInfo(string remotePath)
    {
      RemotePath = remotePath;
    }
  }

  /// <summary>
  /// A mapped network drive on a Windows operating system.
  /// This class simplifies remote file system access by connecting this computer to a shared resource on the 
  /// guest operating system. It performs a function equivalent to the "net use \\&lt;guest ip&gt;\drive$" or 
  /// \\&lt;guest ip&gt;\share" command.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Based on https://github.com/dblock/vmwaretasks/blob/master/Source/VMWareTools/WindowsMappedNetworkDrive.cs
  /// </para>
  /// <para>
  /// A common source of network mapping failure is a wrong Local Security Policy setting for "Network Access: Sharing 
  /// and Security Model for local accounts" on the target guest operating system. Set it to "classic".
  /// </para>
  /// </remarks>
  public class MappedNetworkDrive : IDisposable
  {
    [DllImport("mpr.dll")]
    private static extern int WNetAddConnection2(
      ref NETRESOURCE lpNetResource, string lpPassword, string UserName, int dwFlags);

    [DllImport("mpr.dll")]
    private static extern int WNetCancelConnection2(
      string lpName, int dwFlags, bool fForceConnection);

    [StructLayout(LayoutKind.Sequential)]
    private struct NETRESOURCE
    {
      public int dwScope;
      public int dwType;
      public int dwDisplayType;
      public int dwUsage;
      public string lpLocalName;
      public string lpRemoteName;
      public string lpComment;
      public string lpProvider;
    }

    private const int RESOURCETYPE_DISK = 0x1;

    //The network resource connection should not be remembered. If this flag is set, the operating system will not attempt to restore the connection when the user logs on again.
    //http://msdn.microsoft.com/en-us/library/windows/desktop/aa385413(v=vs.85).aspx
    private const int CONNECT_TEMPORARY = 0x00000004;

    private bool _mapped = false;
    private string _networkPath;
    private MappedNetworkDriveInfo _info;
    private string _ipAddress;

    /// <summary>
    /// Creates an instance of a mapped network drive.
    /// </summary>
    /// <param name="env">Remote environment.</param>
    /// <param name="info">Mapped network drive info.</param>
    public MappedNetworkDrive(string ipAddress, MappedNetworkDriveInfo info)
    {
      _ipAddress = ipAddress;
      _info = info;

      if (_info.Auto)
      {
        MapNetworkDrive();
      }
    }

    /// <summary>
    /// Mapped network path.
    /// </summary>
    public string NetworkPath
    {
      get
      {
        return _networkPath;
      }
    }

    /// <summary>
    /// Map the network resource.
    /// </summary>
    public void MapNetworkDrive()
    {
      if (_mapped)
      {
        UnMapNetworkDrive();
      }

      NETRESOURCE netResource = new NETRESOURCE();
      netResource.dwScope = 2;
      netResource.dwType = RESOURCETYPE_DISK;
      netResource.dwDisplayType = 3;
      netResource.dwUsage = 1;
      netResource.lpRemoteName = GuestPathToNetworkPath(_info.RemotePath);
      netResource.lpLocalName = _info.LocalPath;
      OsTestLogger.WriteLine("Calling WNetAddConnection2 for " + netResource.lpRemoteName);
      int rc = WNetAddConnection2(ref netResource, _info.Password, _info.Username, CONNECT_TEMPORARY);
      if (rc != 0)
      {
        throw new Win32Exception(rc);
      }

      _networkPath = netResource.lpRemoteName;
      _mapped = true;
      OsTestLogger.WriteLine("Succeed with WNetAddConnection2 for " + netResource.lpRemoteName + ", mapped");
    }

    /// <summary>
    /// Unmap a previously mapped network drive.
    /// </summary>
    public void UnMapNetworkDrive()
    {
      if (!_mapped)
      {
        throw new InvalidOperationException();
      }

      OsTestLogger.WriteLine("Calling WNetCancelConnection2 for " + _networkPath);
      int rc = WNetCancelConnection2(_networkPath, 0, true);
      if (rc != 0)
      {
        throw new Win32Exception(rc);
      }

      _mapped = false;
      _networkPath = null;
      OsTestLogger.WriteLine("Succeed with WNetCancelConnection2 for " + _networkPath + ", unmapped");
    }

    /// <summary>
    /// Dispose the object, unmap a previously mapped network drive.
    /// </summary>
    public void Dispose()
    {
      if (_mapped)
      {
        UnMapNetworkDrive();
      }
    }

    /// <summary>
    /// Dispose the object, unmap a previously mapped network drive.
    /// </summary>
    ~MappedNetworkDrive()
    {
      Dispose();
    }

    /// <summary>
    /// Convert a path on the guest operating system to a network IP-based path.
    /// </summary>
    /// <param name="path">Guest operating system path.</param>
    /// <returns>A network path.</returns>
    /// <example>
    /// The following call returns "\\192.168.1.2\c$\temp" on a virtual machine with IP 192.168.1.2.
    /// <code>GuestPathToNetworkPath("C:\temp")</code> 
    /// </example>
    public string GuestPathToNetworkPath(string path)
    {
      return string.Format(@"\\{0}\{1}", _ipAddress,
        path.Replace(":", "$")).TrimEnd(@"\".ToCharArray());
    }
  }
}