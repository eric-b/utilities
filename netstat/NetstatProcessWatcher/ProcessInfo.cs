using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NetstatProcessWatcher
{
    internal sealed class ProcessInfo : EqualityComparer<ProcessInfo>
    {
        public readonly string name, shortName;
        public readonly int id;
        public readonly bool? isWebhost;

        public ProcessInfo(string name)
        {
            this.name = name;
            this.id = 0;
            this.isWebhost = null;
            this.shortName = GetShortName(name, null);
        }

        public ProcessInfo(string name, int id, bool isWebhost)
        {
            this.isWebhost = isWebhost;
            this.name = name;
            this.id = id;
            this.shortName = GetShortName(name, isWebhost);
        }

        public override bool Equals(ProcessInfo x, ProcessInfo y)
        {
            return x.id == y.id;
        }

        public override int GetHashCode(ProcessInfo obj)
        {
            return obj.id;
        }

        private static string GetShortName(string name, bool? isWebhost)
        {
            if (name.Length < 8)
                return name;

            var n = new string(name.Where(t => "aeiou".IndexOf(t) == -1).ToArray());
            n = Regex.Replace(n, @"(srvr|hst)\.?", "", RegexOptions.IgnoreCase).Trim('.');
            n = Regex.Replace(n, @"srvc", "svc", RegexOptions.IgnoreCase).Trim('.');
            n = Regex.Replace(n, @"^\.?\d+\.?", "", RegexOptions.None).Trim('.');
            n = Regex.Replace(n, @"^\W+", "", RegexOptions.None).Trim('.');
            return isWebhost.HasValue && isWebhost.Value ? string.Format("w3wp:{0}", n) : n;
        }
    }
}