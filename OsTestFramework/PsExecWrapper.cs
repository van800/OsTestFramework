using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using JetBrains.OsTestFramework.Common;

namespace JetBrains.OsTestFramework
{
    internal class PsExecWrapper
    {
        private readonly RemoteEnvironment _remoteEnvironment;

        public PsExecWrapper(RemoteEnvironment remoteEnvironment)
        {
            _remoteEnvironment = remoteEnvironment;
        }

        /// <summary>
        ///     Returns host psexec Output+ErrOutPut
        /// </summary>
        /// <param name="command"></param>
        /// <param name="args"></param>
        /// <param name="startTimeout"></param>
        /// <param name="executionTimeout"></param>
        /// <returns></returns>
        public IElevatedCommandResult ExecuteElevatedCommandInGuest(string command, string[] args, TimeSpan startTimeout,
            TimeSpan? executionTimeout = null)
        {
            return ElevatedCommandInGuest("Execute", "", command, args, startTimeout, executionTimeout);
        }

        public IElevatedCommandResult DetachElevatedCommandInGuest(string command, string[] args, TimeSpan startTimeout,
            TimeSpan? executionTimeout = null)
        {
            return ElevatedCommandInGuest("Detach", " -D", command, args, startTimeout, executionTimeout);
        }

        private IElevatedCommandResult ElevatedCommandInGuest(string commandType, string psExecArg, string command, string[] args, TimeSpan startTimeout, TimeSpan? executionTimeout = null)
        {
            using (var processExecutor = new ProcessExecutor(_remoteEnvironment, commandType, psExecArg,
                    command, args, startTimeout, executionTimeout))
            {
                return processExecutor.Execute();
            }
        }

        /// <summary>
        ///     Use ExecuteElevatedCommandInGuest to execute cmd.exe /C "guestCommandLine" > file and return process.
        /// </summary>
        /// <param name="guestCommandLine">Guest command line, argument passed to cmd.exe.</param>
        /// <param name="startTimeout"></param>
        [Obsolete("Switch to DetachElevatedCommandInGuest")]
        public RemoteProcess BeginElevatedCommandInGuest
            (string guestCommandLine, TimeSpan startTimeout)
        {
            DateTime start = DateTime.Now;
            //"psexec.exe \\REMOTECOMPUTER –i –u username –p Password –d yourexe.exe"
            var psi = new ProcessStartInfo(_remoteEnvironment.PsExecPath,
                string.Format("-accepteula \\\\{0} -H -I -U {1} -P \"{2}\" {3}", _remoteEnvironment.IpAddress,
                    _remoteEnvironment.UserName, _remoteEnvironment.Password, guestCommandLine))
            {
                UseShellExecute = false,
                ErrorDialog = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // if before starting process the PSEXESVC doesn't exist - the only child would be the target process
            // if PSEXESVC exists - check the clilds before starting and wait for the new process
            object psExecPId0 = _remoteEnvironment.WmiWrapperInstance.TryGetServicePIdByNameInGuest("PSEXESVC");

            var childs0 = new List<RemoteProcess>();
            if (psExecPId0 != null)
            {
                childs0.AddRange(
                    _remoteEnvironment.WmiWrapperInstance.GetChildProcesses(
                        _remoteEnvironment.WindowsShellInstance, psExecPId0.ToString()));
            }

            OsTestLogger.WriteLine("Start Process: " + psi.FileName + " " + psi.Arguments);
            var localProcess = Process.Start(psi);

            // wait for the new process after running the new one
            RemoteProcess process = null;

            int i = 0;
            while (process == null)
            {
                OsTestLogger.WriteLine("Interation: " + i);
                object psExecPId =
                    _remoteEnvironment.WmiWrapperInstance.TryGetServicePIdByNameInGuest("PSEXESVC");
                if (psExecPId != null && psExecPId.ToString() != "" && psExecPId.ToString() != "0")
                {
                    Thread.Sleep(1000);
                    if (psExecPId0 == null) // new psexec
                    {
                        OsTestLogger.WriteLine("New PsExec started. Expecting single child process.");
                        List<RemoteProcess> possibleProcesses =
                            _remoteEnvironment.WmiWrapperInstance.GetChildProcesses(
                                _remoteEnvironment.WindowsShellInstance, psExecPId.ToString()).ToList();
                        foreach (RemoteProcess possibleProcess in possibleProcesses)
                        {
                            OsTestLogger.WriteLine("possibleProcess: " + possibleProcess.GuestPId);
                        }
                        process = possibleProcesses.SingleOrDefault();
                    }
                    else // psexec was already running
                    {
                        OsTestLogger.WriteLine("PsExec already present with the following child processes:");
                        foreach (RemoteProcess remoteProcess in childs0)
                        {
                            OsTestLogger.WriteLine("PId:" + remoteProcess.GuestPId);
                        }

                        process =
                            _remoteEnvironment.WmiWrapperInstance.GetChildProcesses(
                                _remoteEnvironment.WindowsShellInstance, psExecPId.ToString())
                                .SingleOrDefault(a => !childs0.Contains(a));
                    }
                }
                Thread.Sleep(1000);
                if ((DateTime.Now - start).Duration() > startTimeout)
                {
                    localProcess.Kill();
                    throw new Exception(string.Format("Guest process {0} {1} was not started in {2} seconds.",
                        psi.FileName, psi.Arguments, startTimeout.TotalSeconds));
                }
                i++;
            }

            return process;
        }
    }
}