using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpcDAToMSA.UI.Forms
{
    /// <summary>
    /// 日志显示优化器 - 提供多种优化方案
    /// </summary>
    public class LogDisplayOptimizer
    {
        #region 方案2：基于RichTextBox的彩色日志显示

        /// <summary>
        /// 彩色日志显示方案
        /// </summary>
        public class ColoredLogDisplay
        {
            private readonly RichTextBox richTextBox;
            private readonly Queue<LogEntry> logEntries = new Queue<LogEntry>();
            private readonly object logLock = new object();
            private const int MaxDisplayLines = 1500;
            private Timer updateTimer;

            public ColoredLogDisplay(RichTextBox textBox)
            {
                richTextBox = textBox;
                InitializeTimer();
            }

            private void InitializeTimer()
            {
                updateTimer = new Timer();
                updateTimer.Interval = 100; // 100ms更新一次
                updateTimer.Tick += UpdateDisplay;
            }

            public void AddLog(string message, LogLevel level = LogLevel.Info)
            {
                lock (logLock)
                {
                    logEntries.Enqueue(new LogEntry
                    {
                        Message = message,
                        Level = level,
                        Timestamp = DateTime.Now
                    });

                    // 保持队列大小
                    while (logEntries.Count > MaxDisplayLines * 2)
                    {
                        logEntries.Dequeue();
                    }
                }

                if (!updateTimer.Enabled)
                {
                    updateTimer.Start();
                }
            }

            private void UpdateDisplay(object sender, EventArgs e)
            {
                lock (logLock)
                {
                    if (logEntries.Count == 0)
                    {
                        updateTimer.Stop();
                        return;
                    }

                    // 获取要显示的日志
                    var entriesToShow = logEntries.TakeLast(MaxDisplayLines).ToList();
                    
                    // 清空并重新填充
                    richTextBox.Clear();
                    
                    foreach (var entry in entriesToShow)
                    {
                        AppendColoredLog(entry);
                    }

                    // 滚动到底部
                    richTextBox.SelectionStart = richTextBox.Text.Length;
                    richTextBox.ScrollToCaret();
                }
            }

            private void AppendColoredLog(LogEntry entry)
            {
                var color = GetLogLevelColor(entry.Level);
                var prefix = $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] ";
                
                richTextBox.SelectionStart = richTextBox.TextLength;
                richTextBox.SelectionLength = 0;
                richTextBox.SelectionColor = Color.Gray;
                richTextBox.AppendText(prefix);
                
                richTextBox.SelectionColor = color;
                richTextBox.AppendText(entry.Message + Environment.NewLine);
            }

            private Color GetLogLevelColor(LogLevel level)
            {
                switch (level)
                {
                    case LogLevel.Debug: return Color.Gray;
                    case LogLevel.Info: return Color.LightGreen;
                    case LogLevel.Warning: return Color.Orange;
                    case LogLevel.Error: return Color.Red;
                    case LogLevel.Fatal: return Color.DarkRed;
                    default: return Color.White;
                }
            }

            public void Dispose()
            {
                updateTimer?.Stop();
                updateTimer?.Dispose();
            }
        }

        #endregion

        #region 方案3：基于虚拟化的高性能显示

        /// <summary>
        /// 虚拟化日志显示方案 - 适用于大量日志
        /// </summary>
        public class VirtualizedLogDisplay
        {
            private readonly ListBox listBox;
            private readonly List<LogEntry> allLogs = new List<LogEntry>();
            private readonly object logLock = new object();
            private const int MaxLogs = 10000;
            private int currentStartIndex = 0;
            private int visibleItemCount = 0;

            public VirtualizedLogDisplay(ListBox listBox)
            {
                this.listBox = listBox;
                this.listBox.DrawMode = DrawMode.OwnerDrawVariable;
                this.listBox.DrawItem += ListBox_DrawItem;
                this.listBox.MeasureItem += ListBox_MeasureItem;
            }

            public void AddLog(string message, LogLevel level = LogLevel.Info)
            {
                lock (logLock)
                {
                    allLogs.Add(new LogEntry
                    {
                        Message = message,
                        Level = level,
                        Timestamp = DateTime.Now
                    });

                    // 保持最大日志数量
                    if (allLogs.Count > MaxLogs)
                    {
                        allLogs.RemoveAt(0);
                        if (currentStartIndex > 0)
                            currentStartIndex--;
                    }
                }

                UpdateDisplay();
            }

            private void UpdateDisplay()
            {
                lock (logLock)
                {
                    var visibleLogs = allLogs.Skip(currentStartIndex).Take(visibleItemCount).ToList();
                    
                    listBox.Items.Clear();
                    foreach (var log in visibleLogs)
                    {
                        listBox.Items.Add(log);
                    }

                    // 自动滚动到底部
                    if (listBox.Items.Count > 0)
                    {
                        listBox.TopIndex = listBox.Items.Count - 1;
                    }
                }
            }

            private void ListBox_DrawItem(object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0) return;

                var logEntry = (LogEntry)listBox.Items[e.Index];
                var color = GetLogLevelColor(logEntry.Level);

                e.DrawBackground();
                e.Graphics.DrawString(logEntry.ToString(), listBox.Font, new SolidBrush(color), e.Bounds);
                e.DrawFocusRectangle();
            }

            private void ListBox_MeasureItem(object sender, MeasureItemEventArgs e)
            {
                e.ItemHeight = 16; // 固定行高
            }

            private Color GetLogLevelColor(LogLevel level)
            {
                switch (level)
                {
                    case LogLevel.Debug: return Color.Gray;
                    case LogLevel.Info: return Color.LightGreen;
                    case LogLevel.Warning: return Color.Orange;
                    case LogLevel.Error: return Color.Red;
                    case LogLevel.Fatal: return Color.DarkRed;
                    default: return Color.White;
                }
            }
        }

        #endregion

        #region 方案4：基于异步队列的高性能方案

        /// <summary>
        /// 异步队列日志显示方案
        /// </summary>
        public class AsyncQueueLogDisplay
        {
            private readonly TextBox textBox;
            private readonly Queue<string> logQueue = new Queue<string>();
            private readonly object queueLock = new object();
            private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
            private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            private Task processingTask;

            public AsyncQueueLogDisplay(TextBox textBox)
            {
                this.textBox = textBox;
                StartProcessing();
            }

            public void AddLog(string message)
            {
                lock (queueLock)
                {
                    logQueue.Enqueue(message);
                }
            }

            private async void StartProcessing()
            {
                processingTask = Task.Run(async () =>
                {
                    while (!cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await ProcessLogs();
                        await Task.Delay(50, cancellationTokenSource.Token);
                    }
                });
            }

            private async Task ProcessLogs()
            {
                await semaphore.WaitAsync();
                try
                {
                    var logsToProcess = new List<string>();
                    
                    lock (queueLock)
                    {
                        while (logQueue.Count > 0 && logsToProcess.Count < 100) // 批量处理
                        {
                            logsToProcess.Add(logQueue.Dequeue());
                        }
                    }

                    if (logsToProcess.Count > 0)
                    {
                        await UpdateUI(logsToProcess);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }

            private async Task UpdateUI(List<string> logs)
            {
                if (textBox.InvokeRequired)
                {
                    textBox.Invoke(new Action(() => AppendLogs(logs)));
                }
                else
                {
                    AppendLogs(logs);
                }
            }

            private void AppendLogs(List<string> logs)
            {
                var sb = new StringBuilder();
                foreach (var log in logs)
                {
                    sb.AppendLine(log);
                }

                textBox.AppendText(sb.ToString());
                
                // 保持最大行数
                var lines = textBox.Lines;
                if (lines.Length > 1500)
                {
                    var newLines = lines.Skip(lines.Length - 1500).ToArray();
                    textBox.Lines = newLines;
                }

                // 滚动到底部
                textBox.SelectionStart = textBox.Text.Length;
                textBox.ScrollToCaret();
            }

            public void Dispose()
            {
                cancellationTokenSource.Cancel();
                processingTask?.Wait(1000);
                semaphore?.Dispose();
                cancellationTokenSource?.Dispose();
            }
        }

        #endregion
    }

    /// <summary>
    /// 日志条目
    /// </summary>
    public class LogEntry
    {
        public string Message { get; set; }
        public LogLevel Level { get; set; }
        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] [{Level}] {Message}";
        }
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }
}
