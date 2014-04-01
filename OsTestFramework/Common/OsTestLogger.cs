using System;

namespace JetBrains.OsTestFramework.Common
{
  public static class OsTestLogger
  {
    public static void WriteLine(string content)
    {
      Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + content);
    }
  }
}
