using System.Diagnostics;

namespace SimpleV2Ray
{
    internal class Program
    {
        private readonly ManualResetEvent v2rayExitedEvent = new(false);
        private readonly CancellationTokenSource statsMonThreadCTS = new();
        private readonly DoubleBufferedStatsBuilder statsBuilder = new();
        private readonly ConsoleNative.ConsoleCtrlHandler consoleCtrlHandler;      // Keeps it from getting garbage collected

        private Process? v2rayProc;
        private Thread? statsMonThread;

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

        public Program()
        {
            consoleCtrlHandler = new ConsoleNative.ConsoleCtrlHandler(HandleConsoleCtrl);
            ConsoleNative.SetConsoleCtrlHandler(consoleCtrlHandler, true);
        }

        internal void Run()
        {
            Console.CursorVisible = false;
            Environment.CurrentDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase!;

            try
            {
                // load config
                V2RayConfig? v2RayConfig = V2RayConfig.Load("config.json");
                if (v2RayConfig == null)
                {
                    Console.Error.WriteLine("Failed to load V2Ray config.");
                    return;
                }

                // resolve proxy inbound
                if (v2RayConfig.Inbounds == null)
                {
                    Console.Error.WriteLine("Failed to resolve inbounds.");
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
                    Console.Error.WriteLine("Failed to resolve proxy inbound.");
                    return;
                }

                Console.WriteLine("Configuring system proxy...");
                if (!SystemProxy.SetProxy(proxyUrl, false))
                {
                    Console.Error.WriteLine("Failed to configure system proxy.");
                    return;
                }

                Console.WriteLine("Starting V2Ray...");
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
            catch (Exception)
            {
                OnExit();
                throw;
            }
            OnExit();
        }

        private void V2rayProc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if(e.Data != null && e.Data.EndsWith("[api] "))
            {
                return;
            }
            
            // write log
            Console.Write(new string(' ', Console.BufferWidth) + '\r');
            Console.WriteLine(e.Data);

            // append stats
            AppendStatsFloating();
        }

        private void V2rayProc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            // write log
            Console.Write(new string(' ', Console.BufferWidth) + '\r');
            Console.Error.WriteLine(e.Data);

            // append stats
            AppendStatsFloating();
        }

        private void V2rayProc_Exited(object? sender, EventArgs e)
        {
            Console.WriteLine("V2Ray Exited.");
            v2rayExitedEvent.Set();
        }

        private void StatsMonProc(V2RayConfig v2RayConfig, CancellationToken cancellationToken)
        {
            string v2ctlPath = Path.Combine(Environment.CurrentDirectory, "v2ray-core", "v2ctl.exe");
            List<OutboundStats> outboundStatses = new();

            // resolve api server
            if (v2RayConfig.Api == null || v2RayConfig.Api.Tag == null)
            {
                Console.Error.WriteLine("Failed to resolve API.");
                return;
            }
            string apiTag = v2RayConfig.Api.Tag;

            if (v2RayConfig.Routing == null || v2RayConfig.Routing.Rules == null)
            {
                Console.Error.WriteLine("Failed to resolve routing.");
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
                Console.Error.WriteLine("Failed to resolve API inbound.");
                return;
            }

            if (v2RayConfig.Inbounds == null)
            {
                Console.Error.WriteLine("Failed to resolve inbounds.");
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
                Console.Error.WriteLine("Failed to resolve API server.");
                return;
            }

            // resolve outbounds
            if (v2RayConfig.Outbounds == null)
            {
                Console.Error.WriteLine("Failed to resolve outbounds.");
                return;
            }

            foreach (V2RayConfig.OutboundConfig outbound in v2RayConfig.Outbounds)
            {
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

                    statsBuilder.Swap();

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
            for (int i = 0; i < statsBuilder.NumLines; i++)
            {
                Console.WriteLine(new string(' ', Console.BufferWidth) + '\r');
            }

            Console.SetCursorPosition(0, Console.CursorTop - statsBuilder.NumLines);
            Console.Write(statsBuilder.Text);
            Console.SetCursorPosition(0, Console.CursorTop - statsBuilder.NumLines);
        }

        private bool HandleConsoleCtrl(int eventType)
        {
            Console.WriteLine("Console window closing, death imminent");
            OnExit();
            return false;
        }

        private void OnExit()
        {
            Console.WriteLine("Removing system proxy...");
            if (!SystemProxy.SetProxy(null, false))
            {
                Console.Error.WriteLine("Failed to remove system proxy.");
                return;
            }

            if (IsV2RayRunning)
            {
                Console.WriteLine("Stopping V2Ray...");
                v2rayProc!.Kill();
                v2rayExitedEvent.Set();
            }

            if (IsStatsMonThreadRunning)
            {
                Console.WriteLine("Stopping StatsMon...");
                statsMonThreadCTS.Cancel();
                statsMonThread!.Join();
            }
        }


        static void Main(string[] args)
        {
            Console.WriteLine(string.Join(' ', args));

            Program program = new();
            program.Run();
        }
    }
}