using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RefRemap
{
    public interface ILogger
    {
        Task Log(LogLevel logLevel, string message);
    }
}
