using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleV2Ray
{
    public class Logger
    {
        public readonly object consoleLock = new();

        public void AppendLine(string text)
        {
            lock (consoleLock)
            {
                Console.WriteLine(text);
            }
        }

        public void AppendErrorLine(string text)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Error.WriteLine(text);
                Console.ResetColor();
            }

        }
    }
}
