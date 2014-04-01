using JetBrains.OsTestsFramework.VIX.BaseTests;
using NUnit.Framework;

namespace JetBrains.OsTestsFramework.Test
{
  [TestFixture]
  class OsTestTaskTest
  {
    [Test]
    public void ExecuteTest()
    {
      var task = new OsTestTask { VirtualMachineName = "Server2003x64" };
      Assert.IsTrue(task.Execute());
    }
  }
}
