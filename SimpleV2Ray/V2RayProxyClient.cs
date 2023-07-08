using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleV2Ray
{
    public class V2RayProxyClient
    {
        private readonly Logger logger;
        private readonly ManualResetEvent v2rayExitedEvent = new(false);
        private readonly CancellationTokenSource statsMonThreadCTS = new();
        private readonly DoubleBufferedStatsBuilder statsBuilder = new();

        private Process? v2rayProc;
        private Thread? statsMonThread;

        private string? apiTag = null;

        bool IsV2RayRunning
        {
            get
            {
                return v2rayProc != null && !v2rayProc.HasExited;
            }
        }

        bool IsStatsMonThreadRunning
        {
            get
            {
                return statsMonThread != null && statsMonThread.IsAlive;
            }
        }


        public V2RayProxyClient(Logger logger)
        {
            this.logger = logger;
        }


        public void Start()
        {
            try
            {
                // load config
                V2RayConfig? v2RayConfig = V2RayConfig.Load("config.json");
                if (v2RayConfig == null)
                {
                    logger.AppendErrorLine("Failed to load V2Ray config.");
                    return;
                }

                // resolve proxy inbound
                if (v2RayConfig.Inbounds == null)
                {
                    logger.AppendErrorLine("Failed to resolve inbounds.");
                    return;
                }
                string? proxyUrl = null;
                foreach (V2RayConfig.InboundConfig inbound in v2RayConfig.Inbounds)
                {
                    if (inbound.Protocol == "http")
                    {
                        proxyUrl = $"{inbound.Protocol}://{inbound.Listen}:{inbound.Port}";
                    }
                }
                if (proxyUrl == null)
                {
                    logger.AppendErrorLine("Failed to resolve proxy inbound.");
                    return;
                }

                // resolve api tag
                if (v2RayConfig.Api == null || v2RayConfig.Api.Tag == null)
                {
                    logger.AppendErrorLine("Failed to resolve API Tag.");
                    return;
                }
                apiTag = v2RayConfig.Api.Tag;

                logger.AppendLine("Configuring system proxy...");
                if (!SystemProxy.SetProxy(proxyUrl, false))
                {
                    logger.AppendErrorLine("Failed to configure system proxy.");
                    return;
                }

                logger.AppendLine("Starting V2Ray...");
                v2rayExitedEvent.Reset();
                v2rayProc = new Process()
                {
                    StartInfo = new ProcessStartInfo(Path.Combine(Environment.CurrentDirectory, "v2ray-core", "v2ray.exe"), "-c config.json")
                    {
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                    },
                    EnableRaisingEvents = true,
                };
                v2rayProc.Exited += V2rayProc_Exited;
                v2rayProc.OutputDataReceived += V2rayProc_OutputDataReceived;
                v2rayProc.ErrorDataReceived += V2rayProc_ErrorDataReceived;
                v2rayProc.Start();

                v2rayProc.BeginOutputReadLine();
                v2rayProc.BeginErrorReadLine();

                statsMonThread = new Thread(new ThreadStart(() => StatsMonProc(v2RayConfig, statsMonThreadCTS.Token)));
                statsMonThread.Start();

                v2rayExitedEvent.WaitOne();
            }
            catch (Exception ex)
            {
                logger.AppendErrorLine($"{ex.Message}\n{ex.StackTrace}");
                OnExit();
                throw;
            }
            OnExit();
        }

        public void Stop()
        {

        }

        public void WaitForExit()
        {

        }

        private void V2rayProc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && e.Data.EndsWith($"[{apiTag}]"))
            {
                return;
            }

            // write log
            logger.AppendLine(new string(' ', Console.BufferWidth) + '\r' + e.Data);

            // append stats
            AppendStatsFloating();
        }

        private void V2rayProc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            // write log
            logger.AppendErrorLine(new string(' ', Console.BufferWidth) + '\r' + e.Data);

            // append stats
            AppendStatsFloating();
        }

        private void V2rayProc_Exited(object? sender, EventArgs e)
        {
            if (v2rayProc != null && v2rayProc.ExitCode != 0)
            {
                logger.AppendErrorLine($"V2Ray Exited with code {v2rayProc.ExitCode}.");
            }
            else
            {
                logger.AppendLine("V2Ray Exited.");
            }
            v2rayExitedEvent.Set();
        }

        private void StatsMonProc(V2RayConfig v2RayConfig, CancellationToken cancellationToken)
        {
            string v2ctlPath = Path.Combine(Environment.CurrentDirectory, "v2ray-core", "v2ctl.exe");
            List<OutboundStats> outboundStatses = new();

            // resolve api server
            if (v2RayConfig.Api == null || v2RayConfig.Api.Tag == null)
            {
                logger.AppendErrorLine("Failed to resolve API.");
                return;
            }
            string apiTag = v2RayConfig.Api.Tag;

            if (v2RayConfig.Routing == null || v2RayConfig.Routing.Rules == null)
            {
                logger.AppendErrorLine("Failed to resolve routing.");
                return;
            }
            HashSet<string>? apiInbounds = null;
            foreach (V2RayConfig.RoutingConfig.RuleConfig rule in v2RayConfig.Routing.Rules)
            {
                if (rule.OutboundTag == apiTag && rule.InboundTag != null)
                {
                    apiInbounds = new HashSet<string>(rule.InboundTag);
                    break;
                }
            }
            if (apiInbounds == null)
            {
                logger.AppendErrorLine("Failed to resolve API inbound.");
                return;
            }

            if (v2RayConfig.Inbounds == null)
            {
                logger.AppendErrorLine("Failed to resolve inbounds.");
                return;
            }
            string? apiServer = null;
            foreach (V2RayConfig.InboundConfig inbound in v2RayConfig.Inbounds)
            {
                if (inbound.Tag != null && apiInbounds.Contains(inbound.Tag))
                {
                    apiServer = $"{inbound.Listen}:{inbound.Port}";
                    break;
                }
            }
            if (apiServer == null)
            {
                logger.AppendErrorLine("Failed to resolve API server.");
                return;
            }

            // resolve outbounds
            if (v2RayConfig.Outbounds == null)
            {
                logger.AppendErrorLine("Failed to resolve outbounds.");
                return;
            }

            foreach (V2RayConfig.OutboundConfig outbound in v2RayConfig.Outbounds)
            {
                if (outbound.SendThrough == "127.0.0.1")
                {
                    // skip loopback
                    continue;
                }
                if (outbound.Tag != null)
                {
                    outboundStatses.Add(new OutboundStats(outbound.Tag, apiServer));
                }
            }

            // proc loop
            try
            {
                while (true)
                {
                    statsBuilder.Clear();
                    statsBuilder.AppendLine(null);
                    statsBuilder.AppendLine($"========== Stats {DateTime.Now:yyyy/MM/dd HH:mm:ss} ==========");
                    foreach (OutboundStats outboundStats in outboundStatses)
                    {
                        outboundStats.Update(v2ctlPath);
                        statsBuilder.AppendLine($"{outboundStats.Name}");
                        statsBuilder.AppendLine($" {"UpLink",10}: {BinarySizeFormatter.SizeSuffix(outboundStats.Uplink),20} {BinarySizeFormatter.SizeSuffix(outboundStats.UplinkRate),20}/s");
                        statsBuilder.AppendLine($" {"DownLink",10}: {BinarySizeFormatter.SizeSuffix(outboundStats.Downlink),20} {BinarySizeFormatter.SizeSuffix(outboundStats.DownlinkRate),20}/s");
                    }

                    lock (statsBuilder)
                    {
                        statsBuilder.Swap();
                    }

                    AppendStatsFloating();

                    Thread.Sleep(1000);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {

            }
        }

        private void AppendStatsFloating()
        {
            lock (statsBuilder)
            {
                lock (logger.consoleLock)
                {
                    for (int i = 0; i < statsBuilder.NumLines; i++)
                    {
                        Console.WriteLine(new string(' ', Console.BufferWidth) + '\r');
                    }

                    Console.SetCursorPosition(0, Console.CursorTop - statsBuilder.NumLines);
                    Console.Write(statsBuilder.Text);
                    Console.SetCursorPosition(0, Console.CursorTop - statsBuilder.NumLines);
                }
            }
        }

        private void OnExit()
        {
            logger.AppendLine("Removing system proxy...");
            if (!SystemProxy.SetProxy(null, false))
            {
                logger.AppendErrorLine("Failed to remove system proxy.");
                return;
            }

            if (IsV2RayRunning)
            {
                logger.AppendLine("Stopping V2Ray...");
                v2rayProc!.Kill();
                v2rayExitedEvent.Set();
            }

            if (IsStatsMonThreadRunning)
            {
                logger.AppendLine("Stopping StatsMon...");
                statsMonThreadCTS.Cancel();
                statsMonThread!.Join();
            }
        }

        public void Close()
        {
            OnExit();
        }
    }
}
