using System;
using System.Diagnostics;

namespace JetBrains.OsTestFramework
{
    public interface IAsRemoteUserScopeExecutor
    {
        void Execute(string machineIpAddress, string username, string password, Action action);
    }

    public class AsRemoteUserScopeExecutor : IAsRemoteUserScopeExecutor
    {
        private readonly ICmdKeyRunner _cmdKeyRunner;

        public AsRemoteUserScopeExecutor(ICmdKeyRunner cmdKeyRunner)
        {
            _cmdKeyRunner = cmdKeyRunner;
        }

        public void Execute(string machineIpAddress, string username, string password, Action action)
        {
            try
            {
                _cmdKeyRunner.AddKey(machineIpAddress, username, password);
                action.Invoke();
            }
            finally
            {
                _cmdKeyRunner.DeleteKey(machineIpAddress);
            }
        }
    }
}