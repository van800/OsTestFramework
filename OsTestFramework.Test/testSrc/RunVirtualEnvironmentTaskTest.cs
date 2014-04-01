using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.OsTestsFramework.VIX.Tasks;
using NUnit.Framework;

namespace JetBrains.OsTestsFramework.Test
{
  [TestFixture]
  public class RunVirtualEnvironmentTaskTest
  {
    [Test]
    public void Test()
    {
      var task = new RunVirtualEnvironmentTask {VmName = "Win7x64"};
      task.Execute();
    }
  }
}
