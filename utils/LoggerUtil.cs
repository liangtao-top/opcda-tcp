using Serilog.Core;
using Serilog;
using OPCDA2MSA;

namespace OpcDAToMSA.utils
{
    internal class LoggerUtil
    {

        // 默认日志配置
        public static Logger log = new LoggerConfiguration()
       .MinimumLevel.Verbose()
       .WriteTo.Console()
       .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
       .CreateLogger();

        public static void Configuration(LoggerJson conf)
        {
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration();
            switch (conf.Level.ToLower())
            {
                case "debug":
                    loggerConfiguration.MinimumLevel.Debug();
                    break;
                case "info":
                    loggerConfiguration.MinimumLevel.Information();
                    break;
                case "warn":
                    loggerConfiguration.MinimumLevel.Warning();
                    break;
                case "error":
                    loggerConfiguration.MinimumLevel.Error();
                    break;
                case "fatal":
                    loggerConfiguration.MinimumLevel.Fatal();
                    break;
               default:
                    loggerConfiguration.MinimumLevel.Verbose();
                    break;
            }
            loggerConfiguration.WriteTo.Console();
            if (conf.File != null && conf.File != "")
            {
                loggerConfiguration.WriteTo.File(conf.File, rollingInterval: RollingInterval.Day);
            }
            LoggerUtil.log = loggerConfiguration.CreateLogger();
        }

    }
}
