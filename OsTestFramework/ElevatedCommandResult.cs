using System;

namespace JetBrains.OsTestFramework
{
  public interface IElevatedCommandResult
  {
    string StdOut { get; }
    string StdErr { get; }
    int? ExitCode { get; }
    DateTime? ExitTime { get; }
    bool HasExited { get; }
  }

  public class ElevatedCommandResult : IElevatedCommandResult
  {
    public ElevatedCommandResult(string stdOut, string stdErr, int? exitCode = null, DateTime? exitTime = null, bool hasExited = false)
    {
      StdOut = stdOut;
      StdErr = stdErr;
      ExitCode = exitCode;
      ExitTime = exitTime;
      HasExited = hasExited;
    }

    public string StdOut { get; private set; }
    public string StdErr { get; private set; }
    public int? ExitCode { get; private set; }
    public DateTime? ExitTime { get; private set; }
    public bool HasExited { get; private set; }

    public override string ToString()
    {
      return ("StdErr:" + StdErr + " StdOut:" + StdOut).Replace("\r", " ").Replace("\n", " ") +
        ExitCode ?? (" ExitCode:" + ExitCode) +
        ExitTime ?? (" ExitTime:" + ExitTime) +
        HasExited ?? " HasExited:" + HasExited;
    }
  }
}