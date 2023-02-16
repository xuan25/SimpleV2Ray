using System.Runtime.InteropServices;

namespace SimpleV2Ray
{
    internal static class Native
    {
        [DllImport("wininet.dll")]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct InternetPerConnOptionList
        {
            public int dwSize;
            public IntPtr pszConnection;
            public int dwOptionCount;
            public int dwOptionError;
            public IntPtr pOptions;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct InternetPerConnOption
        {
            public int dwOption;
            public InternetPerConnOptionOptionUnion Value;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InternetPerConnOptionOptionUnion
        {
            [FieldOffset(0)]
            public int dwValue;
            [FieldOffset(0)]
            public IntPtr pszValue;
            [FieldOffset(0)]
            public System.Runtime.InteropServices.ComTypes.FILETIME ftValue;
        }

        public enum InternetOption
        {
            INTERNET_OPTION_PER_CONNECTION_OPTION = 75,
            INTERNET_OPTION_SETTINGS_CHANGED = 39,
            INTERNET_OPTION_REFRESH = 37
        }

        public enum InternetPerConnOptionEnum
        {
            INTERNET_PER_CONN_FLAGS = 1,
            INTERNET_PER_CONN_PROXY_SERVER = 2,
            INTERNET_PER_CONN_PROXY_BYPASS = 3,
            INTERNET_PER_CONN_AUTOCONFIG_URL = 4,
            INTERNET_PER_CONN_AUTODISCOVERY_FLAGS = 5,
            INTERNET_PER_CONN_AUTOCONFIG_SECONDARY_URL = 6,
            INTERNET_PER_CONN_AUTOCONFIG_RELOAD_DELAY_MINS = 7,
            INTERNET_PER_CONN_AUTOCONFIG_LAST_DETECT_TIME = 8,
            INTERNET_PER_CONN_AUTOCONFIG_LAST_DETECT_URL = 9,
            INTERNET_PER_CONN_FLAGS_UI = 10,
            INTERNET_OPTION_PROXY_USERNAME = 43,
            INTERNET_OPTION_PROXY_PASSWORD = 44
        }

        public enum InternetOptionPerConnFlags
        {
            PROXY_TYPE_DIRECT = 0x00000001,   // direct to net
            PROXY_TYPE_PROXY = 0x00000002,   // via named proxy
            PROXY_TYPE_AUTO_PROXY_URL = 0x00000004,   // autoproxy URL
            PROXY_TYPE_AUTO_DETECT = 0x00000008   // use autoproxy detection
        }

        public static IntPtr OptionsToIntPtr(InternetPerConnOption[] options)
        {
            int size = 0;
            for (int i = 0; i < options.Length; i++)
            {
                size += Marshal.SizeOf(options[i]);
            }
            IntPtr buffer = Marshal.AllocCoTaskMem(size);
            IntPtr current = buffer;
            for (int i = 0; i < options.Length; i++)
            {
                Marshal.StructureToPtr(options[i], current, false);
                current = (IntPtr)((long)current + Marshal.SizeOf(options[i]));
            }
            return buffer;
        }
    }
}
