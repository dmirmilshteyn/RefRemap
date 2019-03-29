using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RefRemap
{
    public class ConsoleLogger : ILogger
    {
        public Task Log(LogLevel logLevel, string message) {
            switch (logLevel) {
                case LogLevel.Error:
                    Console.Error.WriteLine(message);
                    break;
                default:
                    Console.WriteLine(message);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
