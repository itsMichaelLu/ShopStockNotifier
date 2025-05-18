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
        private static readonly ConsoleColor[] AvailableColors = Enum.GetValues(typeof(ConsoleColor))
            .Cast<ConsoleColor>()
            .Where(c => c != Console.BackgroundColor && c != ConsoleColor.Black) // skip background and black
            .ToArray();

        private readonly int _id = 0;
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

            if (_id == 0)
            {
                color = ConsoleColor.White; // Thread 1 is always white
            }
            else
            {
                int index = _id % AvailableColors.Length;
                color = AvailableColors[index];
            }
            string time = String.Format("{0,-23}", GetTimeStringNow());
            string pid = String.Format("[OID=0x{0:X4}]    ", _id);

            lock (Console.Out) // prevent output overlap
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine($"{time}{pid}{message}");
                Console.ForegroundColor = originalColor;
            }
        }

        public void LogPadCenter(string message, int padLength, char pad)
        {
            Log(message.PadLeft(((padLength + message.Length) / 2), pad).PadRight(padLength, pad));
        }

    }
}
