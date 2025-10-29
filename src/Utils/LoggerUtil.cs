using Serilog.Core;
using Serilog;
using Serilog.Sinks.Async;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Events;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System;

namespace OpcDAToMSA.Utils
{
    internal class LoggerUtil
    {
        public static IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[] { new KeyValuePair<string, string>("apiKey", "secret-api-key") })
            .Build();

        // 日志实例，通过Configuration方法初始化
        public static Logger log = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.Sink(new FormLogSink())
            .CreateLogger();

        /// <summary>
        /// 配置日志系统
        /// </summary>
        /// <param name="conf">日志配置</param>
        public static void Configuration(LoggerJson conf)
        {
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration();

            // 设置日志级别
            SetLogLevel(loggerConfiguration, conf.Level);

            // 配置异步日志（默认启用）
            loggerConfiguration.WriteTo.Async(a => { }, bufferSize: conf.BufferSize);

            // 配置控制台输出
            if (conf.Console)
            {
                loggerConfiguration.WriteTo.Async(a => a.Console(outputTemplate: conf.Template));
            }

            // 配置文件输出
            if (conf.FileEnabled && conf.Path != null)
            {
                ConfigureFileOutput(loggerConfiguration, conf);
            }

            // 配置窗体日志输出
            loggerConfiguration.WriteTo.Sink(new FormLogSink());

            // 创建新的日志器
            LoggerUtil.log = loggerConfiguration.CreateLogger();
        }

        /// <summary>
        /// 设置日志级别
        /// </summary>
        /// <param name="config">日志配置</param>
        /// <param name="level">级别字符串</param>
        private static void SetLogLevel(LoggerConfiguration config, string level)
        {
            switch (level?.ToLower())
            {
                case "trace":
                    config.MinimumLevel.Verbose();
                    break;
                case "debug":
                    config.MinimumLevel.Debug();
                    break;
                case "info":
                    config.MinimumLevel.Information();
                    break;
                case "warn":
                    config.MinimumLevel.Warning();
                    break;
                case "error":
                    config.MinimumLevel.Error();
                    break;
                case "fatal":
                    config.MinimumLevel.Fatal();
                    break;
                default:
                    config.MinimumLevel.Information();
                    break;
            }
        }

        /// <summary>
        /// 配置文件输出
        /// </summary>
        /// <param name="config">日志配置</param>
        /// <param name="conf">日志配置对象</param>
        private static void ConfigureFileOutput(LoggerConfiguration config, LoggerJson conf)
        {
            var pathConfig = conf.Path;
            if (pathConfig == null) return;

            // 主日志文件
            var mainPath = pathConfig.GetFullPath("info");
            config.WriteTo.Async(a => a.File(
                mainPath,
                outputTemplate: conf.Template,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: conf.MaxFiles,
                fileSizeLimitBytes: ParseFileSize(conf.MaxSize),
                shared: true));

            // 错误日志文件
            var errorPath = pathConfig.GetFullPath("error");
            config.WriteTo.Async(a => a.File(
                errorPath,
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
                outputTemplate: conf.Template,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: conf.MaxFiles,
                fileSizeLimitBytes: ParseFileSize(conf.MaxSize),
                shared: true));

            // 调试日志文件（仅在debug级别时）
            if (conf.Level?.ToLower() == "debug")
            {
                var debugPath = pathConfig.GetFullPath("debug");
                config.WriteTo.Async(a => a.File(
                    debugPath,
                    restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug,
                    outputTemplate: conf.Template,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: conf.MaxFiles,
                    fileSizeLimitBytes: ParseFileSize(conf.MaxSize),
                    shared: true));
            }
        }

        /// <summary>
        /// 解析文件大小字符串为字节数
        /// </summary>
        /// <param name="sizeString">大小字符串 (如: 10MB, 100KB)</param>
        /// <returns>字节数</returns>
        private static long ParseFileSize(string sizeString)
        {
            if (string.IsNullOrEmpty(sizeString))
                return 10 * 1024 * 1024; // 默认10MB

            sizeString = sizeString.ToUpper();
            long multiplier = 1;

            if (sizeString.EndsWith("KB"))
            {
                multiplier = 1024;
                sizeString = sizeString.Substring(0, sizeString.Length - 2);
            }
            else if (sizeString.EndsWith("MB"))
            {
                multiplier = 1024 * 1024;
                sizeString = sizeString.Substring(0, sizeString.Length - 2);
            }
            else if (sizeString.EndsWith("GB"))
            {
                multiplier = 1024 * 1024 * 1024;
                sizeString = sizeString.Substring(0, sizeString.Length - 2);
            }

            if (long.TryParse(sizeString, out long size))
            {
                return size * multiplier;
            }

            return 10 * 1024 * 1024; // 默认10MB
        }
    }

    /// <summary>
    /// 窗体日志Sink，将日志发送到窗体显示
    /// </summary>
    public class FormLogSink : ILogEventSink
    {
        /// <summary>
        /// 获取简短的日志级别字符串
        /// </summary>
        private static string GetShortLevel(Serilog.Events.LogEventLevel level)
        {
            switch (level)
            {
                case Serilog.Events.LogEventLevel.Verbose:
                    return "VRB";
                case Serilog.Events.LogEventLevel.Debug:
                    return "DBG";
                case Serilog.Events.LogEventLevel.Information:
                    return "INF";
                case Serilog.Events.LogEventLevel.Warning:
                    return "WRN";
                case Serilog.Events.LogEventLevel.Error:
                    return "ERR";
                case Serilog.Events.LogEventLevel.Fatal:
                    return "FTL";
                default:
                    return "UNK";
            }
        }

        public void Emit(Serilog.Events.LogEvent logEvent)
        {
            try
            {
                // 格式化日志消息 - 优化级别显示
                var levelStr = GetShortLevel(logEvent.Level);
                var message = $"{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{levelStr}] {logEvent.RenderMessage()}";
                
                // 如果有异常，添加异常信息
                if (logEvent.Exception != null)
                {
                    message += $"\n{logEvent.Exception}";
                }

                // 调试信息 - 输出到控制台
                //Console.WriteLine($"FormLogSink: 发送日志消息: {message}");

                // 通过事件发送日志到窗体
                ApplicationEvents.OnLogMessageReceived(message);
            }
            catch (Exception ex)
            {
                // 调试信息
                Console.WriteLine($"FormLogSink: 发送日志失败: {ex.Message}");
            }
        }
    }
}
