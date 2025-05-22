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
        private const int SPACING = 2;
        private readonly int _id = 0;
        private readonly ConsoleColor _color;
        private static readonly object _consoleLock = new();
        private static readonly object _colorLock = new();
        private static readonly Queue<ConsoleColor> ColorQueue = new(
            Enum.GetValues<ConsoleColor>()
                .Where(c => c != Console.BackgroundColor && c != ConsoleColor.Black && c != ConsoleColor.White)
                .OrderBy(_ => Guid.NewGuid())
        );

        public Logger(int id = 0)
        {
            this._id = id & 0xFFFF;
            _color = GetConsoleColor(id);
        }

        private static string GetTimeStringNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static ConsoleColor GetConsoleColor(int id)
        {
            if (id == 0) return ConsoleColor.White;
            lock (_colorLock)
            {
                var color = ColorQueue.Dequeue();
                ColorQueue.Enqueue(color);
                return color;
            }
        }

        public void Log(string message)
        {
            if (_id != 0) ColorQueue.Enqueue(_color);

            var timePadded = GetTimeStringNow() + new string(' ', SPACING);
            string pidPadded = $"[OID=0x{_id:X4}]" + new string(' ', SPACING); 

            lock (_consoleLock) // prevent output overlap
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = _color;
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
