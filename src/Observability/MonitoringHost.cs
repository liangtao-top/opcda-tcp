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
                return;
            }
            if (!allowReconnect)
            {
                return;
            }
            try
            {
                LoggerUtil.log.Warning($"协议 {Name} 连接断开，开始重连（第{ReconnectAttempts + 1}次，backoff={backoffMs}ms）");
                ReconnectAttempts++;
                var ok = await reconnectAsync().ConfigureAwait(false);
                if (ok)
                {
                    LastOk = DateTime.Now;
                    backoffMs = 2000;
                    LoggerUtil.log.Information($"协议 {Name} 重连成功（累计尝试 {ReconnectAttempts} 次）");
                }
                else
                {
                    backoffMs = Math.Min(backoffMs * 2, maxBackoffMs);
                    LoggerUtil.log.Warning($"协议 {Name} 重连失败，退避至 {backoffMs}ms");
                }
            }
            catch
            {
                backoffMs = Math.Min(backoffMs * 2, maxBackoffMs);
                LoggerUtil.log.Warning($"协议 {Name} 重连异常，退避至 {backoffMs}ms");
            }
            try { await Task.Delay(backoffMs, token).ConfigureAwait(false); } catch { }
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
                return;
            }
            if (!allowReconnect)
            {
                return;
            }
            try
            {
                LoggerUtil.log.Warning($"协议 {Name} 连接断开，开始重连（第{ReconnectAttempts + 1}次，backoff={backoffMs}ms）");
                ReconnectAttempts++;
                var ok = await reconnectAsync().ConfigureAwait(false);
                if (ok)
                {
                    LastOk = DateTime.Now;
                    backoffMs = 2000;
                    LoggerUtil.log.Information($"协议 {Name} 重连成功（累计尝试 {ReconnectAttempts} 次）");
                }
                else
                {
                    backoffMs = Math.Min(backoffMs * 2, maxBackoffMs);
                    LoggerUtil.log.Warning($"协议 {Name} 重连失败，退避至 {backoffMs}ms");
                }
            }
            catch
            {
                backoffMs = Math.Min(backoffMs * 2, maxBackoffMs);
                LoggerUtil.log.Warning($"协议 {Name} 重连异常，退避至 {backoffMs}ms");
            }
            try { await Task.Delay(backoffMs, token).ConfigureAwait(false); } catch { }
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


