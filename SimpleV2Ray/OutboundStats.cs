using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SimpleV2Ray
{
    internal class OutboundStats
    {
        public string Name { get; private set; }
        public long Uplink { get; private set; }
        public long Downlink { get; private set; }
        public double UplinkRate { get; private set; }
        public double DownlinkRate { get; private set; }
        public DateTime LastUpdate { get; private set; }

        public OutboundStats(string name)
        {
            Name = name;
            LastUpdate = DateTime.Now;
        }

        public void Update(string v2ctlPath)
        {
            DateTime now = DateTime.Now;
            TimeSpan deltaTime = now - LastUpdate;
            double deltaSec = deltaTime.TotalSeconds;
            LastUpdate = now;

            long? updatedUplink = GetStats($"outbound>>>{Name}>>>traffic>>>uplink", v2ctlPath);
            if (updatedUplink != null)
            {
                UplinkRate = (updatedUplink.Value - Uplink) / deltaSec;
                Uplink = updatedUplink.Value;
            }
            long? updatedDownlink = GetStats($"outbound>>>{Name}>>>traffic>>>downlink", v2ctlPath);
            if (updatedDownlink != null)
            {
                DownlinkRate = (updatedDownlink.Value - Downlink) / deltaSec;
                Downlink = updatedDownlink.Value;
            }
        }

        private static long? GetStats(string name, string v2ctlPath)
        {
            Process process = new()
            {
                StartInfo = new ProcessStartInfo(v2ctlPath, $"api --server=127.0.0.1:15490 StatsService.GetStats \"name: '{name}' reset: false\"")
                {
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };
            process.Start();
            process.WaitForExit();
            string result = process.StandardOutput.ReadToEnd();

            Match match = Regex.Match(result, $"stat: <\n  name: \"{name}\"\n  value: (?<value>[0-9]+)\n>\n\n");
            if (!match.Success)
            {
                return null;
            }

            long val = long.Parse(match.Groups["value"].Value);

            return val;
        }
    }
}
