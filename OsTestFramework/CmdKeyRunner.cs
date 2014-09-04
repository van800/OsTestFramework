using System;
using System.Diagnostics;

namespace JetBrains.OsTestFramework
{
    public interface ICmdKeyRunner
    {
        void AddKey(string machineIpAddress, string username, string password);
        void DeleteKey(string machineIpAddress);
    }

    public class CmdKeyRunner : ICmdKeyRunner
    {
        public void AddKey(string machineIpAddress, string username, string password)
        {
            string arguments = String.Format(@" /add:{0} /user:{1} /pass:{2}", machineIpAddress, username, password);
            ExecuteCmdKey(arguments);
        }

        public void DeleteKey(string machineIpAddress)
        {
            string arguments = String.Format(@" /delete:{0}", machineIpAddress);
            ExecuteCmdKey(arguments);
        }

        private void ExecuteCmdKey(string arguments)
        {
            Process process = Process.Start("cmdkey.exe", arguments);
            if (process == null) throw new Exception("could not create process cmdkey.exe");
            process.WaitForExit();
        }
    }
}