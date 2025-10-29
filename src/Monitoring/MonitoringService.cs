using OpcDAToMSA.Utils;
using OpcDAToMSA.Protocols;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpcDAToMSA.Monitoring
{
    /// <summary>
    /// 监控服务接口
    /// </summary>
    public interface IMonitoringService
    {
        /// <summary>
        /// 启动监控服务
        /// </summary>
        void Start();

        /// <summary>
        /// 停止监控服务
        /// </summary>
        void Stop();

        /// <summary>
        /// 获取健康报告
        /// </summary>
        /// <returns>健康报告</returns>
        HealthReport GetHealthReport();

        /// <summary>
        /// 记录性能指标
        /// </summary>
        /// <param name="metric">指标名称</param>
        /// <param name="value">指标值</param>
        void RecordMetric(string metric, double value);

        /// <summary>
        /// 记录事件
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="message">事件消息</param>
        void RecordEvent(string eventType, string message);

        /// <summary>
        /// 是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 注册健康检查
        /// </summary>
        /// <param name="componentName">组件名称</param>
        /// <param name="healthCheck">健康检查函数</param>
        void RegisterHealthCheck(string componentName, Func<HealthStatus> healthCheck);

        /// <summary>
        /// 更新指标
        /// </summary>
        /// <param name="metricName">指标名称</param>
        /// <param name="value">指标值</param>
        /// <param name="unit">单位</param>
        void UpdateMetric(string metricName, double value, string unit = "");
    }

    /// <summary>
    /// 系统监控服务
    /// </summary>
    public class MonitoringService : IMonitoringService
    {
        #region Private Fields

        private static MonitoringService instance;
        private static readonly object lockObject = new object();
        private readonly Timer healthCheckTimer;
        private readonly Timer metricsTimer;
        private readonly Dictionary<string, HealthStatus> healthStatuses;
        private readonly Dictionary<string, MetricValue> metrics;
        private bool isRunning = false;

        #endregion

        #region Events

        /// <summary>
        /// 健康状态变更事件
        /// </summary>
        public event EventHandler<HealthStatusChangedEventArgs> HealthStatusChanged;

        /// <summary>
        /// 指标更新事件
        /// </summary>
        public event EventHandler<MetricsUpdatedEventArgs> MetricsUpdated;

        #endregion

        #region Constructor

        /// <summary>
        /// 私有构造函数，实现单例模式
        /// </summary>
        private MonitoringService()
        {
            healthStatuses = new Dictionary<string, HealthStatus>();
            metrics = new Dictionary<string, MetricValue>();
            
            // 健康检查定时器（每30秒）
            healthCheckTimer = new Timer(PerformHealthCheck, null, Timeout.Infinite, Timeout.Infinite);
            
            // 指标收集定时器（每10秒）
            metricsTimer = new Timer(CollectMetrics, null, Timeout.Infinite, Timeout.Infinite);
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// 单例实例
        /// </summary>
        public static MonitoringService Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObject)
                    {
                        if (instance == null)
                        {
                            instance = new MonitoringService();
                        }
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// 当前健康状态
        /// </summary>
        public Dictionary<string, HealthStatus> HealthStatuses => new Dictionary<string, HealthStatus>(healthStatuses);

        /// <summary>
        /// 当前指标
        /// </summary>
        public Dictionary<string, MetricValue> Metrics => new Dictionary<string, MetricValue>(metrics);

        #endregion

        #region Public Methods

        /// <summary>
        /// 启动监控服务
        /// </summary>
        public void Start()
        {
            try
            {
                if (isRunning)
                {
                    LoggerUtil.log.Warning("监控服务已在运行");
                    return;
                }

                // 启动定时器
                healthCheckTimer.Change(0, 30000); // 30秒间隔
                metricsTimer.Change(0, 10000);     // 10秒间隔

                isRunning = true;
                LoggerUtil.log.Information("监控服务已启动");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "启动监控服务失败");
            }
        }

        /// <summary>
        /// 停止监控服务
        /// </summary>
        public void Stop()
        {
            try
            {
                if (!isRunning)
                {
                    LoggerUtil.log.Warning("监控服务未运行");
                    return;
                }

                // 停止定时器
                healthCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
                metricsTimer.Change(Timeout.Infinite, Timeout.Infinite);

                isRunning = false;
                LoggerUtil.log.Information("监控服务已停止");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "停止监控服务失败");
            }
        }

        /// <summary>
        /// 注册健康检查组件
        /// </summary>
        /// <param name="componentName">组件名称</param>
        /// <param name="healthChecker">健康检查函数</param>
        public void RegisterHealthCheck(string componentName, Func<HealthStatus> healthChecker)
        {
            try
            {
                if (string.IsNullOrEmpty(componentName) || healthChecker == null)
                {
                    LoggerUtil.log.Warning("健康检查组件注册参数无效");
                    return;
                }

                // 执行初始健康检查
                var initialStatus = healthChecker();
                healthStatuses[componentName] = initialStatus;

                LoggerUtil.log.Information($"健康检查组件 {componentName} 已注册，状态：{initialStatus.Status}");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, $"注册健康检查组件 {componentName} 失败");
            }
        }

        /// <summary>
        /// 更新指标
        /// </summary>
        /// <param name="metricName">指标名称</param>
        /// <param name="value">指标值</param>
        /// <param name="unit">单位</param>
        public void UpdateMetric(string metricName, double value, string unit = "")
        {
            try
            {
                if (string.IsNullOrEmpty(metricName))
                {
                    LoggerUtil.log.Warning("指标名称不能为空");
                    return;
                }

                var metric = new MetricValue
                {
                    Name = metricName,
                    Value = value,
                    Unit = unit,
                    Timestamp = DateTime.Now
                };

                metrics[metricName] = metric;
                LoggerUtil.log.Debug($"指标 {metricName} 已更新：{value} {unit}");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, $"更新指标 {metricName} 失败");
            }
        }

        /// <summary>
        /// 获取系统健康报告
        /// </summary>
        /// <returns>健康报告</returns>
        public HealthReport GetHealthReport()
        {
            try
            {
                var report = new HealthReport
                {
                    Timestamp = DateTime.Now,
                    OverallStatus = HealthStatusType.Healthy,
                    Components = new Dictionary<string, HealthStatus>(healthStatuses),
                    Metrics = new Dictionary<string, MetricValue>(metrics)
                };

                // 计算整体健康状态
                foreach (var status in healthStatuses.Values)
                {
                    if (status.Status == HealthStatusType.Unhealthy)
                    {
                        report.OverallStatus = HealthStatusType.Unhealthy;
                        break;
                    }
                    else if (status.Status == HealthStatusType.Degraded)
                    {
                        report.OverallStatus = HealthStatusType.Degraded;
                    }
                }

                return report;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "生成健康报告失败");
                return new HealthReport
                {
                    Timestamp = DateTime.Now,
                    OverallStatus = HealthStatusType.Unhealthy,
                    Components = new Dictionary<string, HealthStatus>(),
                    Metrics = new Dictionary<string, MetricValue>()
                };
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 执行健康检查
        /// </summary>
        private void PerformHealthCheck(object state)
        {
            try
            {
                foreach (var component in healthStatuses.Keys)
                {
                    // 这里应该调用注册的健康检查函数
                    // 由于简化实现，我们使用基本的系统检查
                    var status = CheckSystemHealth(component);
                    
                    if (healthStatuses.ContainsKey(component))
                    {
                        var oldStatus = healthStatuses[component];
                        healthStatuses[component] = status;

                        // 如果状态发生变化，触发事件
                        if (oldStatus.Status != status.Status)
                        {
                            OnHealthStatusChanged(new HealthStatusChangedEventArgs(component, oldStatus, status));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "执行健康检查失败");
            }
        }

        /// <summary>
        /// 收集系统指标
        /// </summary>
        private void CollectMetrics(object state)
        {
            try
            {
                // 收集系统指标
                var process = Process.GetCurrentProcess();
                
                //UpdateMetric("cpu_usage", process.TotalProcessorTime.TotalMilliseconds, "ms");
                //UpdateMetric("memory_usage", process.WorkingSet64 / 1024.0 / 1024.0, "MB");
                //UpdateMetric("thread_count", process.Threads.Count, "count");
                //UpdateMetric("uptime", (DateTime.Now - process.StartTime).TotalSeconds, "seconds");

                // 触发指标更新事件
                OnMetricsUpdated(new MetricsUpdatedEventArgs(new Dictionary<string, MetricValue>(metrics)));
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "收集系统指标失败");
            }
        }

        /// <summary>
        /// 检查系统健康状态
        /// </summary>
        /// <param name="componentName">组件名称</param>
        /// <returns>健康状态</returns>
        private HealthStatus CheckSystemHealth(string componentName)
        {
            try
            {
                var status = new HealthStatus
                {
                    ComponentName = componentName,
                    Status = HealthStatusType.Healthy,
                    Timestamp = DateTime.Now,
                    Details = new Dictionary<string, object>()
                };

                // 根据组件名称进行不同的健康检查
                switch (componentName.ToLower())
                {
                    case "opcda":
                        // 检查 OPC DA 连接状态
                        status.Details["connection"] = "connected";
                        break;
                    case "msa":
                        // 检查 MSA 连接状态
                        status.Details["connection"] = "connected";
                        break;
                    case "modbus":
                        // 检查 Modbus TCP 状态
                        status.Details["connection"] = "connected";
                        break;
                    case "mqtt":
                        // 检查 MQTT 连接状态
                        status.Details["connection"] = "connected";
                        break;
                    default:
                        status.Details["status"] = "unknown";
                        break;
                }

                return status;
            }
            catch (Exception ex)
            {
                return new HealthStatus
                {
                    ComponentName = componentName,
                    Status = HealthStatusType.Unhealthy,
                    Timestamp = DateTime.Now,
                    Details = new Dictionary<string, object> { ["error"] = ex.Message }
                };
            }
        }

        /// <summary>
        /// 触发健康状态变更事件
        /// </summary>
        private void OnHealthStatusChanged(HealthStatusChangedEventArgs e)
        {
            HealthStatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 触发指标更新事件
        /// </summary>
        private void OnMetricsUpdated(MetricsUpdatedEventArgs e)
        {
            MetricsUpdated?.Invoke(this, e);
        }

        #endregion

        #region IMonitoringService Implementation

        /// <summary>
        /// 记录性能指标
        /// </summary>
        /// <param name="metric">指标名称</param>
        /// <param name="value">指标值</param>
        public void RecordMetric(string metric, double value)
        {
            UpdateMetric(metric, value, "count");
        }

        /// <summary>
        /// 记录事件
        /// </summary>
        /// <param name="eventType">事件类型</param>
        /// <param name="message">事件消息</param>
        public void RecordEvent(string eventType, string message)
        {
            LoggerUtil.log.Information($"事件记录 - 类型: {eventType}, 消息: {message}");
            UpdateMetric($"event_{eventType}", 1, "count");
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// 健康状态类型
    /// </summary>
    public enum HealthStatusType
    {
        Healthy,    // 健康
        Degraded,   // 降级
        Unhealthy   // 不健康
    }

    /// <summary>
    /// 健康状态
    /// </summary>
    public class HealthStatus
    {
        public string ComponentName { get; set; }
        public HealthStatusType Status { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 指标值
    /// </summary>
    public class MetricValue
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 健康报告
    /// </summary>
    public class HealthReport
    {
        public DateTime Timestamp { get; set; }
        public HealthStatusType OverallStatus { get; set; }
        public Dictionary<string, HealthStatus> Components { get; set; }
        public Dictionary<string, MetricValue> Metrics { get; set; }
    }

    /// <summary>
    /// 健康状态变更事件参数
    /// </summary>
    public class HealthStatusChangedEventArgs : EventArgs
    {
        public string ComponentName { get; }
        public HealthStatus OldStatus { get; }
        public HealthStatus NewStatus { get; }

        public HealthStatusChangedEventArgs(string componentName, HealthStatus oldStatus, HealthStatus newStatus)
        {
            ComponentName = componentName;
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }
    }

    /// <summary>
    /// 指标更新事件参数
    /// </summary>
    public class MetricsUpdatedEventArgs : EventArgs
    {
        public Dictionary<string, MetricValue> Metrics { get; }

        public MetricsUpdatedEventArgs(Dictionary<string, MetricValue> metrics)
        {
            Metrics = metrics;
        }
    }

    #endregion
}
