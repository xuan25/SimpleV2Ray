using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SimpleV2Ray
{
    internal static class SystemProxy
    {
        public static bool SetProxy(string? proxyFullAddr, bool isPAC)
        {
            Native.InternetPerConnOptionList list = new();
            int dwBufSize = Marshal.SizeOf(list);
            // Fill the list structure.
            list.dwSize = Marshal.SizeOf(list);
            // IntPtr.Zero means LAN connection.
            list.pszConnection = IntPtr.Zero;

            if (proxyFullAddr == null)
            {
                Debug.WriteLine("Clearing system proxy");

                list.dwOptionCount = 1;
                Native.InternetPerConnOption[] options = new Native.InternetPerConnOption[1];

                options[0].dwOption = (int)Native.InternetPerConnOptionEnum.INTERNET_PER_CONN_FLAGS;
                options[0].Value.dwValue = (int)Native.InternetOptionPerConnFlags.PROXY_TYPE_DIRECT;

                list.pOptions = Native.OptionsToIntPtr(options);
            }
            else if (isPAC)
            {
                Debug.WriteLine("Setting system proxy for PAC");

                list.dwOptionCount = 2;
                Native.InternetPerConnOption[] options = new Native.InternetPerConnOption[2];

                // Set flags.
                options[0].dwOption = (int)Native.InternetPerConnOptionEnum.INTERNET_PER_CONN_FLAGS;
                options[0].Value.dwValue = (int)(Native.InternetOptionPerConnFlags.PROXY_TYPE_DIRECT | Native.InternetOptionPerConnFlags.PROXY_TYPE_AUTO_PROXY_URL);
                // Set proxy name.
                options[1].dwOption = (int)Native.InternetPerConnOptionEnum.INTERNET_PER_CONN_AUTOCONFIG_URL;
                options[1].Value.pszValue = Marshal.StringToHGlobalAnsi(proxyFullAddr);

                list.pOptions = Native.OptionsToIntPtr(options);
            }
            else
            {
                Debug.WriteLine("Setting system proxy for Global Proxy");

                list.dwOptionCount = 2;
                Native.InternetPerConnOption[] options = new Native.InternetPerConnOption[2];

                // Set flags.
                options[0].dwOption = (int)Native.InternetPerConnOptionEnum.INTERNET_PER_CONN_FLAGS;
                options[0].Value.dwValue = (int)(Native.InternetOptionPerConnFlags.PROXY_TYPE_DIRECT | Native.InternetOptionPerConnFlags.PROXY_TYPE_PROXY);
                // Set proxy name.
                options[1].dwOption = (int)Native.InternetPerConnOptionEnum.INTERNET_PER_CONN_PROXY_SERVER;
                options[1].Value.pszValue = Marshal.StringToHGlobalAnsi(proxyFullAddr);

                list.pOptions = Native.OptionsToIntPtr(options);
            }

            IntPtr intptrStruct = Marshal.AllocCoTaskMem(dwBufSize);
            Marshal.StructureToPtr(list, intptrStruct, true);
            bool bReturn = Native.InternetSetOption(IntPtr.Zero, (int)Native.InternetOption.INTERNET_OPTION_PER_CONNECTION_OPTION, intptrStruct, dwBufSize);

            // Free the allocated memory.
            Marshal.FreeCoTaskMem(list.pOptions);
            Marshal.FreeCoTaskMem(intptrStruct);

            return bReturn;
        }
    }
}
