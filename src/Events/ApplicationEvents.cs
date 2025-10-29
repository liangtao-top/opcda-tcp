using System;

namespace OpcDAToMSA.Events
{
    /// <summary>
    /// 应用程序事件管理器
    /// </summary>
    public static class ApplicationEvents
    {
        // 简单日志缓冲区（启动早期日志在窗体订阅前先入缓冲）
        private static readonly object _logBufferLock = new object();
        private static readonly System.Collections.Generic.List<string> _logBuffer = new System.Collections.Generic.List<string>(256);
        private const int MaxBufferedLogs = 500;
        /// <summary>
        /// OPC连接状态变化事件
        /// </summary>
        public static event EventHandler<OpcConnectionEventArgs> OpcConnectionChanged;

        /// <summary>
        /// MSA连接状态变化事件
        /// </summary>
        public static event EventHandler<MsaConnectionEventArgs> MsaConnectionChanged;

        /// <summary>
        /// 服务状态变化事件
        /// </summary>
        public static event EventHandler<ServiceStatusEventArgs> ServiceStatusChanged;

        /// <summary>
        /// 系统指标更新事件
        /// </summary>
        public static event EventHandler<MetricsUpdatedEventArgs> MetricsUpdated;

        /// <summary>
        /// 日志消息接收事件
        /// </summary>
        public static event EventHandler<LogMessageEventArgs> LogMessageReceived;

        /// <summary>
        /// 触发OPC连接状态变化事件
        /// </summary>
        public static void OnOpcConnectionChanged(bool isConnected, string message = null)
        {
            OpcConnectionChanged?.Invoke(null, new OpcConnectionEventArgs(isConnected, message));
        }

        /// <summary>
        /// 触发MSA连接状态变化事件
        /// </summary>
        public static void OnMsaConnectionChanged(bool isConnected, string message = null)
        {
            MsaConnectionChanged?.Invoke(null, new MsaConnectionEventArgs(isConnected, message));
        }

        /// <summary>
        /// 触发服务状态变化事件
        /// </summary>
        public static void OnServiceStatusChanged(string serviceName, bool isRunning, string message = null)
        {
            ServiceStatusChanged?.Invoke(null, new ServiceStatusEventArgs(serviceName, isRunning, message));
        }

        /// <summary>
        /// 触发系统指标更新事件
        /// </summary>
        public static void OnMetricsUpdated(string metricName, object value, string unit = null)
        {
            MetricsUpdated?.Invoke(null, new MetricsUpdatedEventArgs(metricName, value, unit));
        }

        /// <summary>
        /// 触发日志消息接收事件
        /// </summary>
        public static void OnLogMessageReceived(string message)
        {
            // 先写入缓冲，确保在无订阅者时也能保留
            lock (_logBufferLock)
            {
                _logBuffer.Add(message);
                if (_logBuffer.Count > MaxBufferedLogs)
                {
                    _logBuffer.RemoveRange(0, _logBuffer.Count - MaxBufferedLogs);
                }
            }
            LogMessageReceived?.Invoke(null, new LogMessageEventArgs(message));
        }

        /// <summary>
        /// 获取当前日志缓冲快照
        /// </summary>
        public static string[] GetBufferedLogs()
        {
            lock (_logBufferLock)
            {
                return _logBuffer.ToArray();
            }
        }
    }

    /// <summary>
    /// OPC连接事件参数
    /// </summary>
    public class OpcConnectionEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public OpcConnectionEventArgs(bool isConnected, string message = null)
        {
            IsConnected = isConnected;
            Message = message ?? (isConnected ? "OPC连接成功" : "OPC连接断开");
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// MSA连接事件参数
    /// </summary>
    public class MsaConnectionEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public MsaConnectionEventArgs(bool isConnected, string message = null)
        {
            IsConnected = isConnected;
            Message = message ?? (isConnected ? "MSA连接成功" : "MSA连接断开");
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 服务状态事件参数
    /// </summary>
    public class ServiceStatusEventArgs : EventArgs
    {
        public string ServiceName { get; }
        public bool IsRunning { get; }
        public string Message { get; }
        public DateTime Timestamp { get; }

        public ServiceStatusEventArgs(string serviceName, bool isRunning, string message = null)
        {
            ServiceName = serviceName;
            IsRunning = isRunning;
            Message = message ?? $"{serviceName}服务{(isRunning ? "启动" : "停止")}";
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 系统指标更新事件参数
    /// </summary>
    public class MetricsUpdatedEventArgs : EventArgs
    {
        public string MetricName { get; }
        public object Value { get; }
        public string Unit { get; }
        public DateTime Timestamp { get; }

        public MetricsUpdatedEventArgs(string metricName, object value, string unit = null)
        {
            MetricName = metricName;
            Value = value;
            Unit = unit;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// 日志消息事件参数
    /// </summary>
    public class LogMessageEventArgs : EventArgs
    {
        public string Message { get; }
        public DateTime Timestamp { get; }

        public LogMessageEventArgs(string message)
        {
            Message = message;
            Timestamp = DateTime.Now;
        }
    }
}
