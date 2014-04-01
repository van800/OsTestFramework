using System;
using System.Threading;
using JetBrains.OsTestsFramework.Config.Data.Environment;
using JetBrains.OsTestsFramework.VIX;
using NUnit.Framework;

namespace JetBrains.OsTestsFramework.Test
{
  [TestFixture]
  public class MapPathTest
  {
    [Test]
    public void Test()
    {
      var vm = new VirtualEnvironment("XPx64W");
      vm.CopyFileFromHostToGuest(@"C:\1.txt", @"C:\Temp\1.txt", CopyMethod.network);
      Thread.Sleep(TimeSpan.FromSeconds(2400));
    }
  }
}
