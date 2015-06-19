using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Netstat
{
    public enum NsProtocol
    {
        TCP,
        UDP,
        TCPV6,
        UDPV6
    }

    public enum NsState
    {
        NotApplicable = 0,
        CLOSE_WAIT,
        CLOSED,
        ESTABLISHED,
        FIN_WAIT_1,
        FIN_WAIT_2,
        LAST_ACK,
        LISTENING,
        SYN_RECEIVED,
        SYN_SENT,
        TIME_WAIT
    }

    public static class Netstat
    {
        private static readonly Regex Regex = new Regex(string.Format(@"^\s*(?<proto>{0})\s+(?<local>[\d\.\]\[:]+)\s+(?<remote>[\d\.\]\[:\*]+)\s+(?<state>{1})?\s+(?<pid>\d+).*$", string.Join("|", Enum.GetNames(typeof(NsProtocol))), string.Join("|", Enum.GetNames(typeof(NsState)).Skip(1))), RegexOptions.Multiline | RegexOptions.ExplicitCapture);

        /// <summary>
        /// <para>Launch Windows netstat command and return output.</para>
        /// <remarks>That method may require admin priviledges.</remarks>
        /// </summary>
        /// <param name="filterProtocol"></param>
        /// <param name="displayActiveConnections"></param>
        /// <param name="filterKeywords"></param>
        /// <param name="output">Results</param>
        /// <returns><c>true</c> for success (output contains at least one item).</returns>
        public static bool TryRun(NsProtocol? filterProtocol, bool displayActiveConnections, string[] filterKeywords, out NetstatOutput[] output)
        {
            try
            {
                using (Process cmd = new Process())
                {
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.RedirectStandardInput = true;
                    cmd.StartInfo.RedirectStandardOutput = true;
                    cmd.StartInfo.CreateNoWindow = true;
                    cmd.StartInfo.UseShellExecute = false;

                    cmd.Start();

                    var args = string.Format("-no{0}{1}", displayActiveConnections ? "a" : "", filterProtocol.HasValue ? string.Format("p {0}", filterProtocol.ToString()) : "");
                    if (filterKeywords != null && filterKeywords.Length != 0)
                        cmd.StandardInput.WriteLine(string.Format("netstat {0} | findstr \"{1}\"", args, string.Join(" ", filterKeywords)));
                    else
                        cmd.StandardInput.WriteLine(string.Format("netstat {0}", args));
                    cmd.StandardInput.Flush();
                    cmd.StandardInput.Close();

                    var outputList = new List<NetstatOutput>();
                    var match = Regex.Match(cmd.StandardOutput.ReadToEnd());
                    while (match.Success)
                    {
                        var row = new NetstatOutput();
                        outputList.Add(row);
                        row.Protocol = (NsProtocol)Enum.Parse(typeof(NsProtocol), match.Groups["proto"].Value, false);
                        row.pid = int.Parse(match.Groups["pid"].Value);
                        string local = match.Groups["local"].Value;
                        int index = local.LastIndexOf(':');
                        var localAddress = IPAddress.Parse(local.Substring(0, index));
                        var localPort = int.Parse(local.Substring(index + 1));
                        row.LocalAddress = new IPEndPoint(localAddress, localPort);
                        string state = match.Groups["state"].Value;
                        if (!string.IsNullOrEmpty(state))
                            row.State = (NsState)Enum.Parse(typeof(NsState), state, false);
                        string remote = match.Groups["remote"].Value;
                        if (!string.IsNullOrEmpty(remote) && remote != "*:*")
                        {
                            index = remote.LastIndexOf(':');
                            var remoteAddress = IPAddress.Parse(remote.Substring(0, index));
                            var remotePort = int.Parse(remote.Substring(index + 1));
                            row.RemoteAddress = new IPEndPoint(remoteAddress, remotePort);
                        }
                        match = match.NextMatch();
                    }
                    if (outputList.Count != 0)
                    {
                        output = outputList.ToArray();
                        return true;
                    }
                    else
                    {
                        output = null;
                        return false;
                    }
                }
            }
            catch
            {
                output = null;
                return false;
            }
        }
    }

    public sealed class NetstatOutput
    {
        /// <summary>
        /// UDP or TCP.
        /// </summary>
        public NsProtocol Protocol { get; internal set; }

        /// <summary>
        /// Local end point
        /// </summary>
        public IPEndPoint LocalAddress { get; internal set; }

        /// <summary>
        /// Remote end point (null if UDP).
        /// </summary>
        public IPEndPoint RemoteAddress { get; internal set; }

        /// <summary>
        /// Connection state.
        /// </summary>
        public NsState State { get; internal set; }

        /// <summary>
        /// Process ID.
        /// </summary>
        public int pid { get; set; }

        public override string ToString()
        {
            return string.Format("{0}\t{1}\t{2}\t{3}\t{4}", Protocol, LocalAddress, RemoteAddress, State, pid);
        }
    }
}