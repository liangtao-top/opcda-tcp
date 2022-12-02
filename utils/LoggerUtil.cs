using Serilog.Core;
using Serilog;
using Serilog.Formatting.Json;

namespace OpcDAToMSA.utils
{
    internal class LoggerUtil
    {
        public static readonly Logger log = new LoggerConfiguration()
       .MinimumLevel.Verbose()
       .WriteTo.Console()
       .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
       .CreateLogger();
    }
}
