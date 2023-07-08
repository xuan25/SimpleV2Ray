using System.Diagnostics;

namespace SimpleV2Ray
{
    internal class Program
    {
        private readonly ConsoleNative.ConsoleCtrlHandler consoleCtrlHandler;      // Keeps it from getting garbage collected
        private readonly Logger logger;

        private FileSystemWatcher? configWatcher;
        private V2RayProxyClient? v2RayProxyClient;

        private bool exiting = false;

        private readonly ManualResetEvent reloadExitEvent = new(false);
        private readonly ManualResetEvent finalizedEvent = new(false);

        public Program()
        {
            consoleCtrlHandler = new ConsoleNative.ConsoleCtrlHandler(HandleConsoleCtrl);
            ConsoleNative.SetConsoleCtrlHandler(consoleCtrlHandler, true);

            logger = new Logger();
        }

        internal void Run()
        {
            Console.CursorVisible = false;
            Environment.CurrentDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase!;

            configWatcher = new("./", "config.json");
            configWatcher.Changed += ConfigWatcher_Changed; ;
            configWatcher.EnableRaisingEvents = true;

            while (!exiting)
            {
                v2RayProxyClient = new(logger);
                v2RayProxyClient.Start();

                reloadExitEvent.Reset();
                reloadExitEvent.WaitOne();

                v2RayProxyClient.Close();
            }
            finalizedEvent.Set();
        }

        private void ConfigWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            logger.AppendLine("Config changed. Restarting...");
            reloadExitEvent.Set();
        }

        private bool HandleConsoleCtrl(int eventType)
        {
            exiting = true;
            logger.AppendLine("Console window closing, death imminent");
            reloadExitEvent.Set();
            finalizedEvent.WaitOne();
            return false;
        }

        static void Main(string[] args)
        {
            Console.WriteLine(string.Join(' ', args));

            Program program = new();
            program.Run();
        }
    }
}