using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpcDAToMSA.Utils;

namespace OpcDAToMSA.Observability
{
    public class MetricValue
    {
        public string Name { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public interface IConnectionMonitor
    {
        string Name { get; }
        bool IsConnected { get; }
        DateTime LastOk { get; }
        Task TickAsync(bool allowReconnect, CancellationToken token);
        void RecordOk();
    }

    internal class AdapterMonitor : IConnectionMonitor
    {
        private readonly Func<bool> isConnected;

        public AdapterMonitor(string name, Func<bool> isConnected)
        {
            this.Name = name;
            this.isConnected = isConnected ?? throw new ArgumentNullException(nameof(isConnected));
        }

        public string Name { get; }
        public bool IsConnected => isConnected();
        public DateTime LastOk { get; private set; } = DateTime.MinValue;

        public Task TickAsync(bool allowReconnect, CancellationToken token)
        {
            if (IsConnected)
            {
                LastOk = DateTime.Now;
            }
            return Task.CompletedTask;
        }

        public void RecordOk()
        {
            LastOk = DateTime.Now;
        }
    }

    internal class AdapterReconnectMonitor : IConnectionMonitor
    {
        private readonly Func<bool> isConnected;
        private readonly Func<Task<bool>> reconnectAsync;
        private int backoffMs = 2000;
        private readonly int maxBackoffMs = 15000;
        public int ReconnectAttempts { get; private set; } = 0;
        private bool isReconnecting = false; // 标记是否正在重连，避免重复触发
        private DateTime lastReconnectAttempt = DateTime.MinValue; // 上次重连尝试时间

        public AdapterReconnectMonitor(string name, Func<bool> isConnected, Func<Task<bool>> reconnectAsync)
        {
            this.Name = name;
            this.isConnected = isConnected ?? throw new ArgumentNullException(nameof(isConnected));
            this.reconnectAsync = reconnectAsync ?? throw new ArgumentNullException(nameof(reconnectAsync));
        }

        public string Name { get; }
        public bool IsConnected => isConnected();
        public DateTime LastOk { get; private set; } = DateTime.MinValue;

        public async Task TickAsync(bool allowReconnect, CancellationToken token)
        {
            if (IsConnected)
            {
                LastOk = DateTime.Now;
                backoffMs = 2000;
                isReconnecting = false; // 连接成功后重置标记
                return;
            }
            if (!allowReconnect)
            {
                return;
            }
            
            // 如果正在重连，或距离上次重连尝试不足2秒，跳过本次检查（避免重复触发）
            if (isReconnecting)
            {
                return;
            }
            
            // 防止在重连过程中的短暂断开被误判为新的断开
            var timeSinceLastAttempt = DateTime.Now - lastReconnectAttempt;
            if (timeSinceLastAttempt.TotalSeconds < 2)
            {
                return;
            }
            
            try
            {
                isReconnecting = true; // 标记开始重连
                lastReconnectAttempt = DateTime.Now;
                
                LoggerUtil.log.Warning($"协议 {Name} 连接断开，开始重连（第{ReconnectAttempts + 1}次，backoff={backoffMs}ms）");
                ReconnectAttempts++;
                var ok = await reconnectAsync().ConfigureAwait(false);
                
                // 重连完成后，即使返回成功，也要验证连接状态（因为连接状态可能延迟更新）
                if (ok)
                {
                    // 给连接状态一点时间更新（等待100ms后再次检查）
                    try { await Task.Delay(100, token).ConfigureAwait(false); } catch { }
                    
                    // 再次检查连接状态，确保真的连接成功
                    if (IsConnected)
                    {
                        LastOk = DateTime.Now;
                        backoffMs = 2000;
                        isReconnecting = false; // 确认连接成功后才重置标记
                        LoggerUtil.log.Information($"协议 {Name} 重连成功（累计尝试 {ReconnectAttempts} 次）");
                        return; // 连接成功，立即返回，不执行后续延迟
                    }
                    else
                    {
                        // 重连返回成功，但连接状态检查失败，可能是状态更新延迟
                        LoggerUtil.log.Warning($"协议 {Name} 重连返回成功，但连接状态检查失败，等待状态同步...");
                        // 延长防重复触发时间，给连接状态更多时间更新
                        lastReconnectAttempt = DateTime.Now.AddSeconds(-1); // 设置1秒前，允许下次检查
                    }
                }
                
                // 如果重连失败，或连接状态检查失败，需要延迟再尝试
                if (!ok || !IsConnected)
                {
                    isReconnecting = false; // 重置标记，允许下次检查
                    backoffMs = Math.Min(backoffMs * 2, maxBackoffMs);
                    LoggerUtil.log.Warning($"协议 {Name} 重连失败，退避至 {backoffMs}ms");
                    // 重连失败后，延迟再尝试
                    try { await Task.Delay(backoffMs, token).ConfigureAwait(false); } catch { }
                }
            }
            catch
            {
                isReconnecting = false; // 异常后重置标记
                backoffMs = Math.Min(backoffMs * 2, maxBackoffMs);
                LoggerUtil.log.Warning($"协议 {Name} 重连异常，退避至 {backoffMs}ms");
                try { await Task.Delay(backoffMs, token).ConfigureAwait(false); } catch { }
            }
        }

        public void RecordOk()
        {
            LastOk = DateTime.Now;
        }
    }

    public class OpcMonitor : IConnectionMonitor
    {
        private readonly Func<bool> isConnected;
        private readonly Func<Task<bool>> reconnectAsync;
        private int backoffMs = 2000;
        private readonly int maxBackoffMs = 15000;
        public int ReconnectAttempts { get; private set; } = 0;
        private bool isReconnecting = false; // 标记是否正在重连，避免重复触发
        private DateTime lastReconnectAttempt = DateTime.MinValue; // 上次重连尝试时间

        public OpcMonitor(string name, Func<bool> isConnected, Func<Task<bool>> reconnectAsync)
        {
            this.Name = name;
            this.isConnected = isConnected ?? throw new ArgumentNullException(nameof(isConnected));
            this.reconnectAsync = reconnectAsync ?? throw new ArgumentNullException(nameof(reconnectAsync));
        }

        public string Name { get; }
        public bool IsConnected => isConnected();
        public DateTime LastOk { get; private set; } = DateTime.MinValue;

        public async Task TickAsync(bool allowReconnect, CancellationToken token)
        {
            if (IsConnected)
            {
                LastOk = DateTime.Now;
                backoffMs = 2000;
                isReconnecting = false; // 连接成功后重置标记
                return;
            }
            if (!allowReconnect)
            {
                return;
            }
            
            // 如果正在重连，或距离上次重连尝试不足2秒，跳过本次检查（避免重复触发）
            if (isReconnecting)
            {
                return;
            }
            
            // 防止在重连过程中的短暂断开被误判为新的断开
            var timeSinceLastAttempt = DateTime.Now - lastReconnectAttempt;
            if (timeSinceLastAttempt.TotalSeconds < 2)
            {
                return;
            }
            
            try
            {
                isReconnecting = true; // 标记开始重连
                lastReconnectAttempt = DateTime.Now;
                
                LoggerUtil.log.Warning($"协议 {Name} 连接断开，开始重连（第{ReconnectAttempts + 1}次，backoff={backoffMs}ms）");
                ReconnectAttempts++;
                var ok = await reconnectAsync().ConfigureAwait(false);
                
                // 重连完成后，即使返回成功，也要验证连接状态（因为连接状态可能延迟更新）
                if (ok)
                {
                    // 给连接状态一点时间更新（等待100ms后再次检查）
                    try { await Task.Delay(100, token).ConfigureAwait(false); } catch { }
                    
                    // 再次检查连接状态，确保真的连接成功
                    if (IsConnected)
                    {
                        LastOk = DateTime.Now;
                        backoffMs = 2000;
                        isReconnecting = false; // 确认连接成功后才重置标记
                        LoggerUtil.log.Information($"协议 {Name} 重连成功（累计尝试 {ReconnectAttempts} 次）");
                        return; // 连接成功，立即返回，不执行后续延迟
                    }
                    else
                    {
                        // 重连返回成功，但连接状态检查失败，可能是状态更新延迟
                        LoggerUtil.log.Warning($"协议 {Name} 重连返回成功，但连接状态检查失败，等待状态同步...");
                        // 延长防重复触发时间，给连接状态更多时间更新
                        lastReconnectAttempt = DateTime.Now.AddSeconds(-1); // 设置1秒前，允许下次检查
                    }
                }
                
                // 如果重连失败，或连接状态检查失败，需要延迟再尝试
                if (!ok || !IsConnected)
                {
                    isReconnecting = false; // 重置标记，允许下次检查
                    backoffMs = Math.Min(backoffMs * 2, maxBackoffMs);
                    LoggerUtil.log.Warning($"协议 {Name} 重连失败，退避至 {backoffMs}ms");
                    // 重连失败后，延迟再尝试
                    try { await Task.Delay(backoffMs, token).ConfigureAwait(false); } catch { }
                }
            }
            catch
            {
                isReconnecting = false; // 异常后重置标记
                backoffMs = Math.Min(backoffMs * 2, maxBackoffMs);
                LoggerUtil.log.Warning($"协议 {Name} 重连异常，退避至 {backoffMs}ms");
                try { await Task.Delay(backoffMs, token).ConfigureAwait(false); } catch { }
            }
        }

        public void RecordOk()
        {
            LastOk = DateTime.Now;
        }
    }

    internal class SystemSampler
    {
        private TimeSpan lastCpuTotal = TimeSpan.Zero;
        private DateTime lastCpuTime = DateTime.MinValue;

        public IDictionary<string, MetricValue> Sample()
        {
            var now = DateTime.Now;
            var dict = new Dictionary<string, MetricValue>();

            // Process CPU percent (approx)
            try
            {
                var proc = Process.GetCurrentProcess();
                var total = proc.TotalProcessorTime;
                if (lastCpuTime != DateTime.MinValue)
                {
                    var dt = (now - lastCpuTime).TotalMilliseconds;
                    var cpu = (total - lastCpuTotal).TotalMilliseconds / dt * 100.0 / Environment.ProcessorCount;
                    dict["sys.proc.cpu.percent"] = NewMetric("sys.proc.cpu.percent", Clamp(cpu, 0, 100), "%", now);
                }
                lastCpuTotal = total; lastCpuTime = now;
            }
            catch { }

            // Process memory (MB)
            try
            {
                var proc = Process.GetCurrentProcess();
                var memMb = proc.WorkingSet64 / 1024.0 / 1024.0;
                dict["sys.proc.memory.mb"] = NewMetric("sys.proc.memory.mb", memMb, "MB", now);
            }
            catch { }

            // System disk free percent (C:)
            try
            {
                var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase));
                if (drive != null)
                {
                    var free = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                    var total = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
                    var percent = total > 0 ? (free / total * 100.0) : 0;
                    dict["sys.disk.c.free.percent"] = NewMetric("sys.disk.c.free.percent", percent, "%", now);
                    dict["sys.disk.c.free.gb"] = NewMetric("sys.disk.c.free.gb", free, "GB", now);
                }
            }
            catch { }

            return dict;
        }

        private static MetricValue NewMetric(string name, double value, string unit, DateTime ts)
        {
            return new MetricValue { Name = name, Value = value, Unit = unit, Timestamp = ts };
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);
    }

    public class MonitoringHost
    {
        private readonly ConcurrentDictionary<string, MetricValue> metrics = new ConcurrentDictionary<string, MetricValue>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IConnectionMonitor> monitors = new List<IConnectionMonitor>();
        private readonly SystemSampler sampler = new SystemSampler();
        private Timer sampleTimer;
        private Timer pushTimer;
        private CancellationTokenSource cts;

        public bool IsRunning { get; private set; }
        private volatile bool reconnectPaused = false;

        public event EventHandler<IDictionary<string, MetricValue>> MetricsPushed;

        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            cts = new CancellationTokenSource();

            sampleTimer = new Timer(_ => DoSample(), null, 0, 10000);
            pushTimer = new Timer(_ => DoPush(), null, 2000, 2000);

            _ = Task.Run(async () => await ConnectionLoop(cts.Token));
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            try { cts?.Cancel(); } catch { }
            try { sampleTimer?.Dispose(); } catch { }
            try { pushTimer?.Dispose(); } catch { }
            cts = null; sampleTimer = null; pushTimer = null;
        }

        public void AttachOpc(string name, Func<bool> isConnected, Func<Task<bool>> reconnectAsync)
        {
            monitors.Add(new OpcMonitor(name, isConnected, reconnectAsync));
        }

        public void AttachAdapter(string name, Func<bool> isConnected)
        {
            monitors.Add(new AdapterMonitor(name, isConnected));
        }

        public void AttachAdapter(string name, Func<bool> isConnected, Func<Task<bool>> reconnectAsync)
        {
            monitors.Add(new AdapterReconnectMonitor(name, isConnected, reconnectAsync));
        }

        private void DoSample()
        {
            var snap = sampler.Sample();
            foreach (var kv in snap)
            {
                metrics[kv.Key] = kv.Value;
            }

            // Connection states as metrics
            foreach (var m in monitors)
            {
                metrics[$"{m.Name}.connected"] = new MetricValue
                {
                    Name = $"{m.Name}.connected",
                    Value = m.IsConnected ? 1 : 0,
                    Unit = "bool",
                    Timestamp = DateTime.Now
                };
                if (m is OpcMonitor om)
                {
                    metrics[$"{m.Name}.reconnect.count"] = new MetricValue
                    {
                        Name = $"{m.Name}.reconnect.count",
                        Value = om.ReconnectAttempts,
                        Unit = "count",
                        Timestamp = DateTime.Now
                    };
                }
                if (m is AdapterReconnectMonitor arm)
                {
                    metrics[$"{m.Name}.reconnect.count"] = new MetricValue
                    {
                        Name = $"{m.Name}.reconnect.count",
                        Value = arm.ReconnectAttempts,
                        Unit = "count",
                        Timestamp = DateTime.Now
                    };
                }
            }
        }

        private void DoPush()
        {
            var snapshot = new Dictionary<string, MetricValue>(metrics);
            MetricsPushed?.Invoke(this, snapshot);
        }

        private async Task ConnectionLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var m in monitors)
                {
                    try { await m.TickAsync(allowReconnect: IsRunning && !reconnectPaused, token).ConfigureAwait(false); }
                    catch { }
                }
                try { await Task.Delay(1000, token).ConfigureAwait(false); } catch { }
            }
        }

        public void PauseReconnect()
        {
            reconnectPaused = true;
        }

        public void ResumeReconnect()
        {
            reconnectPaused = false;
        }
    }
}


