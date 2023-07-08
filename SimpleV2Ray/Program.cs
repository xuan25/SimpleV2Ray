using System.Diagnostics;

namespace SimpleV2Ray
{
    internal class Program
    {
        private readonly ConsoleNative.ConsoleCtrlHandler consoleCtrlHandler;      // Keeps it from getting garbage collected
        private readonly Logger logger;

        private V2RayProxyClient v2RayProxyClient;

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

            v2RayProxyClient = new V2RayProxyClient(logger);
            v2RayProxyClient.Start();
            v2RayProxyClient.WaitForExit();
        }

        private bool HandleConsoleCtrl(int eventType)
        {
            logger.AppendLine("Console window closing, death imminent");
            v2RayProxyClient.Close();
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