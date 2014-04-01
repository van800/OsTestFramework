using System.IO;
using JetBrains.OsTestsFramework.VIX;
using NUnit.Framework;

namespace JetBrains.OsTestsFramework.Test
{
  [TestFixture]
  public class ParseRegValueTest
  {
    [Test]
    public void Test()
    {
      var output = GuestShellExtension.ParseRegValue(@"

HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\10.0
    InstallDir    REG_SZ    C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\

", "InstallDir");
      Assert.AreEqual(@"C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\", output);
    }

    [Test]
    public void Test02()
    {
      //Manually run C:\Windows\SysWOW64\reg.exe QUERY HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\10.0 /v InstallDir >C:\1.txt
      //before test
      var output = GuestShellExtension.ParseRegValue(File.ReadAllText(@"C:\1.txt"), "InstallDir");
      Assert.AreEqual(@"C:\Program Files (x86)\Microsoft Visual Studio 10.0\Common7\IDE\", output);
    }
  }
}
