using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NLog;
using Netstat;

namespace NetstatProcessWatcher
{
    internal class DefaultWatcher : IDisposable
    {
        protected readonly List<ProcessInfo> m_Processes;

        protected readonly Timer m_Timer;
        protected readonly ILogger m_Logger;
        protected readonly int m_TimerInterval;

        protected int m_DisposeCount;

        public DefaultWatcher(ILogger logger, TimeSpan interval)
        {
            m_Logger = logger;
            m_TimerInterval = (int)interval.TotalMilliseconds;
            m_Processes = new List<ProcessInfo>(); 
            m_Timer = new Timer(TimerCallback, null, 0, Timeout.Infinite);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        { 
            if (disposing)
            {
                if (Interlocked.Increment(ref m_DisposeCount) != 1)
                    return;

                m_Timer.Dispose();
            }
        }

        protected Process GetProcessbyIdOrNull(int pid)
        {
            try
            {
                return Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        protected virtual void WriteOutput(IEnumerable<NetstatOutput> output, List<ProcessInfo> processInfos)
        {
            m_Logger.Info("\r\n{0:HH:mm:ss\t(dd/MM)}\r\n", DateTime.Now);
            var groups = output.GroupBy(t => t.pid).OrderByDescending(t => output.Count(p => p.pid == t.Key)).ToArray();
            foreach (var group in groups)
            {
                var info = processInfos.FirstOrDefault(t => t.id == group.Key);
                if (info == null)
                    continue;
                m_Logger.Debug("{0}", string.Join(Environment.NewLine, group.Select(t => string.Format("{0} {1}  {2}  {3}  {5} {4}", t.Protocol, t.LocalAddress, t.RemoteAddress, t.State, info.shortName, t.pid))));
                m_Logger.Info("{3} {4}:\r\n\tListen: {0} Established: {1} Other: {2}", group.Count(t => t.State == NsState.LISTENING), group.Count(t => t.State == NsState.ESTABLISHED), group.Count(t => t.State != NsState.LISTENING && t.State != NsState.ESTABLISHED), group.Key, info.name);
            }

            if (groups.Length > 1)
                m_Logger.Info("Total: Listen: {0} Established: {1} Other: {2}", output.Count(t => t.State == NsState.LISTENING), output.Count(t => t.State == NsState.ESTABLISHED), output.Count(t => t.State != NsState.LISTENING && t.State != NsState.ESTABLISHED));
        }
        
        protected virtual void TimerCallback(object state)
        {
            if (m_DisposeCount != 0)
                return;
            try
            {
                NetstatOutput[] output;
                if (Netstat.Netstat.TryRun(NsProtocol.TCP, true, null, out output))
                {
                    int[] nsPids = output.Select(t => t.pid).Distinct().ToArray();
                    ProcessInfo[] oldItems = m_Processes.Where(t => !nsPids.Contains(t.id)).ToArray();
                    foreach (var item in oldItems)
                        m_Processes.Remove(item);
                    foreach (int pid in nsPids)
                    {
                        if (!m_Processes.Any(t => t.id == pid))
                        {
                            Process p = GetProcessbyIdOrNull(pid);
                            m_Processes.Add(new ProcessInfo(p != null ? p.ProcessName : "(terminated)", pid, false));
                        }
                    }
                    WriteOutput(output, m_Processes);
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
    }
}