using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Web.Administration;
using NLog;
using Netstat;

namespace NetstatProcessWatcher
{
    internal sealed class ProcessWatcher : DefaultWatcher
    {
        private static readonly Regex DigitsRe = new Regex(@"^\d+$");
        private readonly ServerManager _srvMgr;

        public ProcessWatcher(ILogger logger, TimeSpan interval, params string[] processNames) : base(logger, interval)
        {
            foreach (var name in processNames)
                m_Processes.Add(new ProcessInfo(name));

            _srvMgr = new ServerManager();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _srvMgr.Dispose();
            }
        }

        protected override void TimerCallback(object state)
        {
            if (m_DisposeCount != 0)
                return;
            try
            {
                CheckProcessInfos();
                string[] pids = m_Processes.Where(t => t.id != 0).Select(t => t.id.ToString()).ToArray();
                NetstatOutput[] output;
                if (Netstat.Netstat.TryRun(NsProtocol.TCP, true, pids, out output))
                {
                    WriteOutput(output.Where(t => m_Processes.Any(p => p.id == t.pid)), m_Processes);
                }
            }
            catch (Exception ex)
            {
                m_Logger.Error(ex);
                Environment.Exit(1);
            }
            finally
            {
                if (m_DisposeCount == 0)
                {
                    try
                    {
                        m_Timer.Change(m_TimerInterval, Timeout.Infinite);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
            }
        }

        private static bool CheckIfProcessStillExists(int pid)
        {
            try
            {
                return Process.GetProcessById(pid) != null;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private bool TryIdentifyAppPoolProcessId(string appPoolName, out int pid)
        {
            pid = 0;
            foreach (WorkerProcess workerProcess in _srvMgr.WorkerProcesses)
            {
                if (workerProcess.AppPoolName.Equals(appPoolName))
                {
                    pid = workerProcess.ProcessId;
                    return true;
                }
            }

            return false;
        }

        private bool TryIdentifyProcessId(string name, bool? isWebHost, out int pid, out bool webHost)
        {
            pid = 0;
            webHost = false;

            if (isWebHost.HasValue)
            {
                if (isWebHost.Value)
                {
                    webHost = true;
                    return TryIdentifyAppPoolProcessId(name, out pid);
                }
                else
                {
                    webHost = false;
                    var processes = DigitsRe.IsMatch(name) ? new Process[] { Process.GetProcessById(int.Parse(name)) } : Process.GetProcessesByName(name);
                    if (processes.Length == 0)
                    {
                        return false;
                    }
                    else if (processes.Length != 1)
                        m_Logger.Warn("Processes found for name '{0}': {1}. Ignore all but first.", name, string.Join(", ", processes.Select(t => t.Id)));

                    pid = processes[0].Id;
                    return true;
                }
            }
            else
            {
                var processes = DigitsRe.IsMatch(name) ? new Process[] { Process.GetProcessById(int.Parse(name)) } : Process.GetProcessesByName(name);
                if (processes.Length != 0)
                {
                    if (processes.Length != 1)
                        m_Logger.Warn("Processes found for name '{0}': {1}. Ignore all but first.", name, string.Join(", ", processes.Select(t => t.Id)));

                    pid = processes[0].Id;
                    webHost = false;
                    return true;
                }
                else
                {
                    if (TryIdentifyAppPoolProcessId(name, out pid))
                    {
                        webHost = true;
                        return true;
                    }
                    else
                        return false;
                }
            }
        }

        private void CheckProcessInfos() 
        {
            bool changed = false;
            for (int i = 0; i < m_Processes.Count; i++)
            {
                var item = m_Processes[i];
                if (item.id == 0 || !CheckIfProcessStillExists(item.id))
                {
                    int id;
                    bool isWebHost;
                    if (TryIdentifyProcessId(item.name, item.isWebhost, out id, out isWebHost))
                    {
                        changed = true;
                        m_Processes[i] = new ProcessInfo(item.name, id, isWebHost);
                        m_Logger.Info("Process {0} (old id: {1}, new id: {2})", item.name, item.id, id);
                    }
                    else
                    {
                        changed = true;
                        m_Processes[i] = new ProcessInfo(item.name, 0, isWebHost);
                        m_Logger.Info("Process unknown: {0} (old id: {1})", item.name, item.id);
                    }
                }
            }
            if (changed)
            {
                m_Logger.Info("{0}", string.Join(Environment.NewLine, m_Processes.Where(t => t.id != 0).Select(t => string.Format("{0}:\t PID {1}", t.name, t.id))));
            }
        }
    }
}