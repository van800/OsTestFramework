using JetBrains.OsTestsFramework.VIX;
using NUnit.Framework;
using System;
using JetBrains.OsTestsFramework;

namespace JetBrains.OsTestsFramework.Test
{
  [TestFixture]
  class MultiAgentEnvironmentTest
  {
    [Test]
    public void PowerOnEnvFromScratch()
    {
      using (var multiAgentEnv = new MultiAgentEnvironment("Test"))
      {
      }
      //TODO: Assert VM is disposed successfully
    }
  }
}
