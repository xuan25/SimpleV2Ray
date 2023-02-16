using System.Runtime.InteropServices;

namespace SimpleV2Ray
{
    internal class ConsoleNative
    {
        public delegate bool ConsoleCtrlHandler(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler callback, bool add);
    }
}
