using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShopStockNotifier
{
    public class Logger
    {
        private readonly int _id = 0;
        private static readonly object _consoleLock = new();

        private static readonly ConsoleColor[] AvailableColors = Enum.GetValues(typeof(ConsoleColor))
            .Cast<ConsoleColor>()
            .Where(c => c != Console.BackgroundColor && c != ConsoleColor.Black) // skip background and black
            .ToArray();

        private const int spacing = 2;

        public Logger(int id = 0)
        {
            this._id = id & 0xFFFF; 
        }

        private static string GetTimeStringNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public void Log(string message)
        {
            ConsoleColor color = (_id == 0)
            ? ConsoleColor.White
            : AvailableColors[_id % AvailableColors.Length];

            var timePadded = GetTimeStringNow() + new string(' ', spacing);
            string pidPadded = $"[OID=0x{_id:X4}]" + new string(' ', spacing); 

            lock (_consoleLock) // prevent output overlap
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine($"{timePadded}{pidPadded}{message}");
                Console.ForegroundColor = originalColor;
            }
        }

        public void LogConfig(string left, string right, int offset = 0, int align = -20)
        {
            // We must do it this way because {} requires compile time constant for pad
            var left2 = string.Format("{0," + align + "}", left);
            var indent = offset == 0 ? "" : new string(' ', offset);
            Log($"{indent}{left2}:{right}");
            
        }
        public void LogHeader(string message = "", char pad = '=', int length = 70)
        {
            LogPadCenter(message, length, pad);
        }

        public void LogPadCenter(string message, int padLength, char pad)
        {
            Log(message.PadLeft(((padLength + message.Length) / 2), pad).PadRight(padLength, pad));
        }

    }
}
