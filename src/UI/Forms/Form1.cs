using System;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using OpcDAToMSA.Utils;
using OpcDAToMSA.Events;
using Newtonsoft.Json;
using OpcDAToMSA.Services;
using System.Linq;
using OpcDAToMSA.Properties;
using System.Drawing;
using System.Runtime.InteropServices;
using NHotkey.WindowsForms;
using NHotkey;
using OpcDAToMSA.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
// using OpcDAToMSA.Monitoring; // 已移除旧监测服务
using OpcDAToMSA.Observability;

namespace OpcDAToMSA.UI.Forms
{
    public partial class Form1 : Form
    {
        // opcNet 字段已移除，现在使用依赖注入的 serviceManager
        private readonly IServiceManager serviceManager; // 新的服务管理器
        private readonly IConfigurationService configurationService;

        private ToolStripMenuItem startMenuItem;

        private ToolStripMenuItem stopMenuItem;

        // 使用全局版本管理器

        public Form1(IServiceManager serviceManager, IConfigurationService configurationService)
        {
            //初始化Windows窗口组件
            InitializeComponent();
            this.serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            this.configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            //加载时 取消跨线程检查
            Control.CheckForIllegalCrossThreadCalls = false;
            //加载任务栏托盘
            MainNotifyIcon();
            //设置全局热键
            GlobalHotkey();
            //订阅应用程序事件
            SubscribeToApplicationEvents();

            // 回放启动阶段缓冲的日志
            try
            {
                var buffered = ApplicationEvents.GetBufferedLogs();
                if (buffered != null && buffered.Length > 0)
                {
                    foreach (var line in buffered)
                    {
                        ShowContent(line);
                    }
                }
            }
            catch { }
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            this.label1.Focus();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Form1_ClientHeight = this.ClientSize.Height;
            //UpdateBottomPanelHeight();
            //UpdateBottomPanelLayout();
            try
            {
                InitializePointsFromConfig();
                InitializeMonitoringTab();
                // 工业暗黑主题背景
                var darkBg = Color.FromArgb(0x1E, 0x1E, 0x1E);
                this.tabPagePoints.BackColor = darkBg;
                this.tabPageMonitor.BackColor = darkBg;
            }
            catch { }
            
            if (serviceManager != null)
            {
                // 这里可以通过服务管理器获取配置
                // 暂时保持原有逻辑，后续可以优化
                try
                {
                    // 使用配置服务获取配置
                    if (configurationService.GetConfiguration().AutoStart)
                    {
                        this.StartButton_Click(sender, e);
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtil.log.Error(ex, "自动启动检查失败");
                }
            }
        }

        //委托
        protected delegate void ShowContentDelegate(string content);

        // 实时日志逐项渲染（ListBox）
        private ListBox logListBox;
        private const int MaxDisplayLines = 3000;
        private const int TrimBlockSize = 100; // 超限时成块裁剪，避免频繁重排
        private bool autoScrollEnabled = true;

        // 点位表格相关
        private DataGridView pointsGrid;
        private BindingList<PointRow> pointRows;
        private readonly Dictionary<string, PointRow> tagToRow = new Dictionary<string, PointRow>(StringComparer.OrdinalIgnoreCase);

        // 监测表格相关
        private DataGridView metricsGrid;
        private BindingList<MetricRow> metricRows;
        private readonly Dictionary<string, MetricRow> nameToMetric = new Dictionary<string, MetricRow>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 初始化实时日志ListBox
        /// </summary>
        private void EnsureLogList()
        {
            if (logListBox != null) return;
            logListBox = new ListBox();
            logListBox.DrawMode = DrawMode.OwnerDrawFixed;
            // 使用等宽字体，确保列对齐
            try
            {
                logListBox.Font = new Font("Consolas", 9F);
            }
            catch
            {
                // 回退到常见等宽字体
                logListBox.Font = new Font("Courier New", 9F);
            }
            // 基于字体动态设置行高，避免上下挤压
            var lineHeight = TextRenderer.MeasureText("A", logListBox.Font).Height + 2;
            logListBox.ItemHeight = Math.Max(16, lineHeight);
            logListBox.BackColor = Color.Black;
            logListBox.ForeColor = Color.LightGray;
            logListBox.BorderStyle = BorderStyle.None;
            // 容器分区：日志放入中心面板，自动填充
            logListBox.Dock = DockStyle.Fill;
            logListBox.DrawItem += LogListBox_DrawItem;
            logListBox.MouseWheel += LogListBox_MouseWheel;
            logListBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.End)
                {
                    autoScrollEnabled = true;
                    ScrollToBottom();
                }
            };
            // 添加到中心面板并置顶
            this.panelCenter.Controls.Add(logListBox);
            logListBox.BringToFront();
        }

        /// <summary>
        /// 初始化点位列表（从配置registers生成）
        /// </summary>
        private void InitializePointsFromConfig()
        {
            EnsurePointsGrid();
            pointRows = new BindingList<PointRow>();
            tagToRow.Clear();
            var regs = configurationService.GetConfiguration()?.Registers;
            if (regs != null)
            {
                foreach (var kv in regs)
                {
                    var row = new PointRow
                    {
                        Tag = kv.Key,
                        Code = kv.Value,
                        Value = null,
                        Quality = "N/A",
                        Timestamp = DateTime.MinValue,
                        LastChanged = DateTime.MinValue
                    };
                    pointRows.Add(row);
                    tagToRow[kv.Key] = row;
                }
            }
            pointsGrid.DataSource = pointRows;
        }

        /// <summary>
        /// 创建点位表格
        /// </summary>
        private void EnsurePointsGrid()
        {
            if (pointsGrid != null) return;
            pointsGrid = new DataGridView();
            pointsGrid.Dock = DockStyle.Fill;
            pointsGrid.AllowUserToAddRows = false;
            pointsGrid.AllowUserToDeleteRows = false;
            pointsGrid.ReadOnly = true;
            pointsGrid.RowHeadersVisible = false;
            pointsGrid.AutoGenerateColumns = false;
            pointsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            pointsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            ApplyIndustrialTheme(pointsGrid);
            pointsGrid.CellFormatting += PointsGrid_CellFormatting;

            // 定义列
            var colTag = new DataGridViewTextBoxColumn{ DataPropertyName = nameof(PointRow.Tag), HeaderText = "Tag", FillWeight = 30 }; 
            var colCode = new DataGridViewTextBoxColumn{ DataPropertyName = nameof(PointRow.Code), HeaderText = "Code", FillWeight = 18 }; 
            var colValue = new DataGridViewTextBoxColumn{ DataPropertyName = nameof(PointRow.Value), HeaderText = "Value", FillWeight = 18 }; 
            var colQuality = new DataGridViewTextBoxColumn{ DataPropertyName = nameof(PointRow.Quality), HeaderText = "Quality", FillWeight = 12 }; 
            var colTs = new DataGridViewTextBoxColumn{ DataPropertyName = nameof(PointRow.Timestamp), HeaderText = "Timestamp", FillWeight = 22, DefaultCellStyle = new DataGridViewCellStyle{ Format = "yyyy-MM-dd HH:mm:ss" } }; 
            pointsGrid.Columns.AddRange(new DataGridViewColumn[]{ colTag, colCode, colValue, colQuality, colTs });

            // 加入“点位”页
            this.tabPagePoints.Controls.Add(pointsGrid);
        }

        private void PointsGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (pointsGrid?.Rows == null || e.RowIndex < 0 || e.RowIndex >= pointsGrid.Rows.Count) return;
            if (pointsGrid.Rows[e.RowIndex].DataBoundItem is PointRow row)
            {
                // 统一状态色
                ApplyStatusColor(e, row.Quality);

                // 变更高亮（最近2秒）
                if (row.LastChanged != DateTime.MinValue && (DateTime.Now - row.LastChanged).TotalSeconds < 2)
                {
                    e.CellStyle.BackColor = Color.FromArgb(40, 80, 40); // 淡绿色背景
                }
            }
        }

        /// <summary>
        /// 工业暗黑主题：统一表格样式
        /// </summary>
        private void ApplyIndustrialTheme(DataGridView grid)
        {
            if (grid == null) return;
            grid.BackgroundColor = Color.FromArgb(0x1E, 0x1E, 0x1E);
            grid.GridColor = Color.FromArgb(0x2A, 0x2A, 0x2A);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0x26, 0x26, 0x26);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0xF0, 0xF0, 0xF0);
            try { grid.ColumnHeadersDefaultCellStyle.Font = new Font("Consolas", 9F, FontStyle.Bold); } catch { }
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(0x1A, 0x1A, 0x1A);
            grid.DefaultCellStyle.BackColor = Color.FromArgb(0x20, 0x20, 0x20);
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(0xD8, 0xD8, 0xD8);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0x33, 0x33, 0x33);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleVertical;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        }

        /// <summary>
        /// 统一状态着色（Good/Bad/Uncertain）
        /// </summary>
        private void ApplyStatusColor(DataGridViewCellFormattingEventArgs e, string quality)
        {
            if (string.IsNullOrEmpty(quality)) return;
            if (string.Equals(quality, "Good", StringComparison.OrdinalIgnoreCase))
            {
                e.CellStyle.ForeColor = Color.LawnGreen; // #7CFC00
            }
            else if (string.Equals(quality, "Bad", StringComparison.OrdinalIgnoreCase))
            {
                e.CellStyle.ForeColor = Color.OrangeRed;
                e.CellStyle.Font = new Font(e.CellStyle.Font ?? this.Font, FontStyle.Bold);
            }
            else if (!string.Equals(quality, "N/A", StringComparison.OrdinalIgnoreCase))
            {
                e.CellStyle.ForeColor = Color.Orange;
            }
        }

        /// <summary>
        /// 对外暴露的点位更新入口（后续可由OPC数据流调用）
        /// </summary>
        public void UpdatePointValue(string tag, object value, string quality, DateTime timestamp)
        {
            if (string.IsNullOrEmpty(tag)) return;
            if (!tagToRow.TryGetValue(tag, out var row)) return;
            // 在UI线程更新
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdatePointValue(tag, value, quality, timestamp)));
                return;
            }
            row.Value = value;
            row.Quality = quality ?? row.Quality;
            row.Timestamp = timestamp == default ? DateTime.Now : timestamp;
            row.LastChanged = DateTime.Now;
            // 局部刷新本行
            var index = pointRows.IndexOf(row);
            if (index >= 0)
            {
                pointsGrid.InvalidateRow(index);
            }
        }

        /// <summary>
        /// 初始化监测页：表格与事件订阅
        /// </summary>
        private void InitializeMonitoringTab()
        {
            EnsureMetricsGrid();
            metricRows = new BindingList<MetricRow>();
            nameToMetric.Clear();
            metricsGrid.DataSource = metricRows;
            try
            {
                // 订阅新 MonitoringHost（并行）
                var host = serviceManager?.GetService<MonitoringHost>();
                if (host != null)
                {
                    host.MetricsPushed += OnHostMetricsPushed;
                }
            }
            catch { }
        }

        private void EnsureMetricsGrid()
        {
            if (metricsGrid != null) return;
            metricsGrid = new DataGridView();
            metricsGrid.Dock = DockStyle.Fill;
            metricsGrid.AllowUserToAddRows = false;
            metricsGrid.AllowUserToDeleteRows = false;
            metricsGrid.ReadOnly = true;
            metricsGrid.RowHeadersVisible = false;
            metricsGrid.AutoGenerateColumns = false;
            metricsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            metricsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            ApplyIndustrialTheme(metricsGrid);

            var cZh = new DataGridViewTextBoxColumn{ DataPropertyName = nameof(MetricRow.DisplayName), HeaderText = "Name", FillWeight = 28 };
            var cName = new DataGridViewTextBoxColumn{ DataPropertyName = nameof(MetricRow.Name), HeaderText = "Metric", FillWeight = 28 };
            var cVal = new DataGridViewTextBoxColumn{ DataPropertyName = nameof(MetricRow.Value), HeaderText = "Value", FillWeight = 22, DefaultCellStyle = new DataGridViewCellStyle{ Format = "0.###" } };
            var cUnit = new DataGridViewTextBoxColumn{ DataPropertyName = nameof(MetricRow.Unit), HeaderText = "Unit", FillWeight = 10 };
            var cTs = new DataGridViewTextBoxColumn{ DataPropertyName = nameof(MetricRow.Timestamp), HeaderText = "Timestamp", FillWeight = 12, DefaultCellStyle = new DataGridViewCellStyle{ Format = "yyyy-MM-dd HH:mm:ss" } };
            metricsGrid.Columns.AddRange(new DataGridViewColumn[]{ cZh, cName, cVal, cUnit, cTs });

            this.tabPageMonitor.Controls.Add(metricsGrid);
        }

        private void OnServiceMetricsUpdated(object sender, OpcDAToMSA.Events.MetricsUpdatedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnServiceMetricsUpdated(sender, e)));
                return;
            }
            if (e == null || string.IsNullOrEmpty(e.MetricName)) return;
            var name = (e.MetricName ?? string.Empty).ToLowerInvariant();
            if (!nameToMetric.TryGetValue(name, out var row))
            {
                row = new MetricRow{ Name = name };
                nameToMetric[name] = row;
                metricRows.Add(row);
            }
            double dv = 0;
            if (e.Value is IConvertible)
            {
                try { dv = Convert.ToDouble(e.Value); } catch { dv = 0; }
            }
            row.Value = dv;
            row.Unit = e.Unit;
            row.Timestamp = e.Timestamp;
            row.DisplayName = GetMetricDisplayName(name);
            metricsGrid?.Invalidate();
        }

        private string GetMetricDisplayName(string metric)
        {
            if (string.IsNullOrEmpty(metric)) return metric;
            switch (metric.ToLower())
            {
                case "sys.proc.cpu.percent": return "CPU(%)";
                case "sys.proc.memory.mb": return "内存(MB)";
                case "sys.disk.c.free.percent": return "磁盘可用(%)";
                case "sys.disk.c.free.gb": return "磁盘可用(GB)";
                case "opc.connected": return "OPC-连接";
                case "opc.reconnect.count": return "OPC-重连次数";
            }
            if (metric.EndsWith(".connected", StringComparison.OrdinalIgnoreCase))
            {
                var prefix = metric.Split('.')[0];
                return $"{prefix.ToUpper()}-连接";
            }
            if (metric.EndsWith(".reconnect.count", StringComparison.OrdinalIgnoreCase))
            {
                var prefix = metric.Split('.')[0];
                return $"{prefix.ToUpper()}-重连次数";
            }
            return metric;
        }

        private void OnHostMetricsPushed(object sender, IDictionary<string, OpcDAToMSA.Observability.MetricValue> e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnHostMetricsPushed(sender, e)));
                return;
            }
            if (e == null) return;
            foreach (var kv in e)
            {
                var m = kv.Value;
                var key = (m.Name ?? string.Empty).ToLowerInvariant();
                if (!nameToMetric.TryGetValue(key, out var row))
                {
                    row = new MetricRow{ Name = key };
                    nameToMetric[key] = row;
                    metricRows.Add(row);
                }
                row.Value = m.Value;
                row.Unit = m.Unit;
                row.Timestamp = m.Timestamp;
                row.DisplayName = GetMetricDisplayName(key);
            }
            metricsGrid?.Invalidate();
        }

        private void ReorderTopMetrics()
        {
            if (metricRows == null || metricRows.Count == 0) return;
            string[] order = new []
            {
                "sys.proc.cpu.percent",
                "sys.proc.memory.mb",
                "sys.disk.c.free.percent",
                "sys.disk.c.free.gb"
            };
            // suspend grid layout to avoid heavy redraw
            try { metricsGrid?.SuspendLayout(); } catch { }
            metricRows.RaiseListChangedEvents = false;
            for (int target = 0; target < order.Length; target++)
            {
                var key = order[target];
                if (!nameToMetric.TryGetValue(key, out var row)) continue;
                int idx = metricRows.IndexOf(row);
                if (idx >= 0 && idx != target)
                {
                    metricRows.RemoveAt(idx);
                    if (target > metricRows.Count) target = metricRows.Count;
                    metricRows.Insert(target, row);
                }
            }
            metricRows.RaiseListChangedEvents = true;
            metricRows.ResetBindings();
            try { metricsGrid?.ResumeLayout(false); } catch { }
        }

        private void LogListBox_MouseWheel(object sender, MouseEventArgs e)
        {
            // 用户滚动则暂停自动滚动；当滚动回到底部再恢复
            autoScrollEnabled = IsAtBottom();
        }

        private bool IsAtBottom()
        {
            if (logListBox.Items.Count == 0) return true;
            int visible = Math.Max(1, logListBox.ClientSize.Height / Math.Max(1, logListBox.ItemHeight));
            return logListBox.TopIndex >= Math.Max(0, logListBox.Items.Count - visible - 1);
        }

        private void ScrollToBottom()
        {
            if (logListBox.Items.Count > 0)
            {
                logListBox.TopIndex = logListBox.Items.Count - 1;
            }
        }

        private void LogListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= logListBox.Items.Count) return;
            string text = logListBox.Items[e.Index]?.ToString() ?? string.Empty;
            // 根据级别着色
            Color color = Color.LightGray;
            if (text.Contains("[ERR]")) color = Color.Red;
            else if (text.Contains("[WRN]")) color = Color.Orange;
            else if (text.Contains("[DBG]")) color = Color.Gray;
            else if (text.Contains("[FTL]")) color = Color.DarkRed;
            else if (text.Contains("[INF]")) color = Color.LightGreen;

            using (var brush = new SolidBrush(color))
            {
                e.Graphics.DrawString(text, logListBox.Font, brush, e.Bounds);
            }
            e.DrawFocusRectangle();
        }

        

        /// <summary>
        /// 逐项追加一条日志
        /// </summary>
        private void ShowContent(string content)
        {
            EnsureLogList();
            bool stickToBottom = autoScrollEnabled && IsAtBottom();

            // 容量控制：超过上限成块删除头部
            if (logListBox.Items.Count >= MaxDisplayLines)
            {
                int removeCount = Math.Min(TrimBlockSize, logListBox.Items.Count);
                for (int i = 0; i < removeCount; i++) logListBox.Items.RemoveAt(0);
            }

            logListBox.Items.Add(content);

            if (stickToBottom)
            {
                ScrollToBottom();
            }
        }

        #region   拦截Windows消息
        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_CLOSE = 0xF060;
            //const int WM_DESTROY = 0x0002;
            if (m.Msg == WM_SYSCOMMAND && (int)m.WParam == SC_CLOSE)
            {
                //捕捉关闭窗体消息      
                //   User   clicked   close   button      
                this.Hide();
                return;
            }
            base.WndProc(ref m);
        }
        #endregion

        #region   TextBox控件跟随窗口变化自动缩放
        private void NotifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Show();
            this.Activate();
        }

        private double Form1_ClientHeight;

        private void Form1_Resize(object sender, EventArgs e)
        {
            //int windowBorder = (this.Width - this.ClientRectangle.Width) / 2;
            //int screenY = (this.Height - this.ClientRectangle.Height - windowBorder);
            //LoggerUtil.log.Debug("Form1_Resize screenY: {@screenY} windowBorder: {@windowBorder}", screenY, windowBorder);
            //LoggerUtil.log.Debug("Form1_Resize Form1_ClientHeight: {@Form1_ClientHeight}", this.Form1_ClientHeight);
            var scale = GetScreenScalingFactor();
            //LoggerUtil.log.Debug("Height: {@Height}|{@Height}, {@b}, scale: {@scale}", this.Form1_ClientHeight, this.ClientSize.Height, this.Form1_ClientHeight.Equals(this.ClientSize.Height), scale);
            // 容器自动布局，无需手动调整日志区域尺寸
        }

        [DllImport("gdi32.dll", EntryPoint = "GetDeviceCaps", SetLastError = true)]
        public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
        enum DeviceCap
        {
            VERTRES = 10,
            PHYSICALWIDTH = 110,
            SCALINGFACTORX = 114,
            DESKTOPVERTRES = 117,

            // http://pinvoke.net/default.aspx/gdi32/GetDeviceCaps.html
        }
        private static double GetScreenScalingFactor()
        {
            var g = Graphics.FromHwnd(IntPtr.Zero);
            IntPtr desktop = g.GetHdc();
            var physicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES);
            var screenScalingFactor = (double)physicalScreenHeight / Screen.PrimaryScreen.Bounds.Height;
            return screenScalingFactor;
        }
        #endregion

        private void StartButton_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            startMenuItem.Enabled = false;
            
            // 使用新的服务管理器
            // serviceManager 已经通过构造函数注入
            
            Thread thread = new Thread(new ThreadStart(async () =>
            {
                try
                {
                    var result = await serviceManager.StartAllServicesAsync();
                    if (result)
                    {
                        this.Invoke(new Action(() =>
                        {
                            okButton.Enabled = true;
                            stopMenuItem.Enabled = true;
                        }));
                    }
                    else
                    {
                        LoggerUtil.log.Error("服务启动失败");
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtil.log.Error(ex, "启动服务时发生异常");
                }
            }));
            thread.Start();
        }

        private async void StopButton_Click(object sender, EventArgs e)
        {
            try
            {
                // 先禁用按钮，防止重复点击
                okButton.Enabled = false;
                stopMenuItem.Enabled = false;
                
                // 使用新的服务管理器停止服务
                if (serviceManager != null)
                {
                    await serviceManager.StopAllServicesAsync().ConfigureAwait(true);
                }
                
                // 重新启用启动按钮
                button1.Enabled = true;
                startMenuItem.Enabled = true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "停止服务失败");
                // 即使失败也要恢复按钮状态
                okButton.Enabled = false;
                stopMenuItem.Enabled = false;
                button1.Enabled = true;
                startMenuItem.Enabled = true;
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            // 清理资源（当前无定时器资源需要清理）
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 清理资源（当前无定时器资源需要清理）
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            //LoggerUtil.log.Debug(e.KeyCode.ToString());
            switch (e.KeyCode)
            {
                case Keys.F9:
                    this.StartButton_Click(sender, e);
                    break;
                case Keys.F10:
                    this.StopButton_Click(sender, e);
                    break;
                case Keys.F11:
                    AboutBox1 aboutBox = new AboutBox1();
                    aboutBox.Show();
                    break;
                case Keys.F12:
                    this.Close();//关闭窗体
                    Application.Exit();//强制所有消息中止，退出所有的窗体，但是若有托管线程（非主线程），也无法干净地退出；
                    Environment.Exit(0);//这是最彻底的退出方式，不管什么线程都被强制退出，把程序结束的很干净。
                    System.Diagnostics.Process.GetCurrentProcess().Kill();//结束整个进程
                    break;
            }
        }

        //任务栏托盘
        private void MainNotifyIcon()
        {
            var cm = new ContextMenuStrip();

            startMenuItem = new ToolStripMenuItem("启动", Resources.qidong, new EventHandler(delegate (object sender, EventArgs e)
            {
                this.StartButton_Click(sender, e);
            }), Keys.F9);
            stopMenuItem = new ToolStripMenuItem("停止", Resources.tingzhi, new EventHandler(delegate (object sender, EventArgs e)
            {
                this.StopButton_Click(sender, e);
            }), Keys.F10);
            cm.Items.Add(startMenuItem);
            cm.Items.Add(stopMenuItem);
            cm.Items.Add(new ToolStripSeparator());
            cm.Items.Add(new ToolStripMenuItem("关于", Resources.about, new EventHandler(delegate (object sender, EventArgs e)
            {
                AboutBox1 aboutBox = new AboutBox1();
                aboutBox.Show();
            }), Keys.F11));
            cm.Items.Add(new ToolStripMenuItem("退出", Resources.tuichu, new EventHandler(delegate (object sender, EventArgs e)
            {
                this.Close();
                Application.Exit();
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                Environment.Exit(0);
            }), Keys.F12));
            this.notifyIcon1.ContextMenuStrip = cm;
        }

        //注册全局热键
        private void GlobalHotkey()
        {
            HotkeyManager.Current.AddOrReplace("Start", Keys.Control | Keys.F9, delegate (object sender, HotkeyEventArgs e)
            {
                Form1_KeyDown(sender, new KeyEventArgs(Keys.F9));
            });
            HotkeyManager.Current.AddOrReplace("Stop", Keys.Control | Keys.F10, delegate (object sender, HotkeyEventArgs e)
            {
                Form1_KeyDown(sender, new KeyEventArgs(Keys.F10));
            });
            HotkeyManager.Current.AddOrReplace("About", Keys.Control | Keys.F11, delegate (object sender, HotkeyEventArgs e)
            {
                Form1_KeyDown(sender, new KeyEventArgs(Keys.F11));
            });
            HotkeyManager.Current.AddOrReplace("Exit", Keys.Control | Keys.F12, delegate (object sender, HotkeyEventArgs e)
            {
                Form1_KeyDown(sender, new KeyEventArgs(Keys.F12));
            });
        }

        #region 事件订阅

        /// <summary>
        /// 订阅应用程序事件
        /// </summary>
        private void SubscribeToApplicationEvents()
        {
            ApplicationEvents.OpcConnectionChanged += OnOpcConnectionChanged;
            ApplicationEvents.MsaConnectionChanged += OnMsaConnectionChanged;
            ApplicationEvents.ServiceStatusChanged += OnServiceStatusChanged;
            ApplicationEvents.MetricsUpdated += OnMetricsUpdated;
            ApplicationEvents.LogMessageReceived += OnLogMessageReceived;
        }

        /// <summary>
        /// 取消订阅应用程序事件
        /// </summary>
        private void UnsubscribeFromApplicationEvents()
        {
            ApplicationEvents.OpcConnectionChanged -= OnOpcConnectionChanged;
            ApplicationEvents.MsaConnectionChanged -= OnMsaConnectionChanged;
            ApplicationEvents.ServiceStatusChanged -= OnServiceStatusChanged;
            ApplicationEvents.MetricsUpdated -= OnMetricsUpdated;
            ApplicationEvents.LogMessageReceived -= OnLogMessageReceived;
        }

        /// <summary>
        /// OPC连接状态变化事件处理
        /// </summary>
        private void OnOpcConnectionChanged(object sender, OpcConnectionEventArgs e)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => OnOpcConnectionChanged(sender, e)));
                    return;
                }

                LoggerUtil.log.Information($"OPC连接状态变化: {e.IsConnected} - {e.Message}");
                
                // 更新OPC DA标签显示
                this.label2.Text = $"OpcDA：{e.IsConnected}";
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "处理OPC连接状态变化事件失败");
            }
        }

        /// <summary>
        /// MSA连接状态变化事件处理
        /// </summary>
        private void OnMsaConnectionChanged(object sender, MsaConnectionEventArgs e)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => OnMsaConnectionChanged(sender, e)));
                    return;
                }

                LoggerUtil.log.Information($"Gateway连接状态变化: {e.IsConnected} - {e.Message}");
                
                // 更新OPC DA标签显示
                this.label1.Text = $"GW：{e.IsConnected}";
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "处理MSA连接状态变化事件失败");
            }
        }

        /// <summary>
        /// 服务状态变化事件处理
        /// </summary>
        private void OnServiceStatusChanged(object sender, ServiceStatusEventArgs e)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => OnServiceStatusChanged(sender, e)));
                    return;
                }

                LoggerUtil.log.Information($"服务状态变化: {e.ServiceName} - {e.IsRunning} - {e.Message}");

                // 服务启动后尝试附加新监测Host
                if (e.IsRunning)
                {
                    var host = serviceManager?.GetService<OpcDAToMSA.Observability.MonitoringHost>();
                    if (host != null)
                    {
                        try
                        {
                            host.MetricsPushed -= OnHostMetricsPushed; // 防重复
                        }
                        catch { }
                        host.MetricsPushed += OnHostMetricsPushed;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "处理服务状态变化事件失败");
            }
        }

        /// <summary>
        /// 系统指标更新事件处理
        /// </summary>
        private void OnMetricsUpdated(object sender, OpcDAToMSA.Events.MetricsUpdatedEventArgs e)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => OnMetricsUpdated(sender, e)));
                    return;
                }

                LoggerUtil.log.Debug($"系统指标更新: {e.MetricName} = {e.Value} {e.Unit}");
                
                // 可以在这里更新系统监控相关的UI
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "处理系统指标更新事件失败");
            }
        }

        /// <summary>
        /// 日志消息接收事件处理
        /// </summary>
        private void OnLogMessageReceived(object sender, LogMessageEventArgs e)
        {
            try
            {
                // 调试信息 - 输出到控制台
                Console.WriteLine($"Form1: 接收到日志消息: {e.Message}");

                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => OnLogMessageReceived(sender, e)));
                    return;
                }

                ShowContent(e.Message);
            }
            catch (Exception ex)
            {
                // 避免日志循环，这里不记录日志
                Console.WriteLine($"处理日志消息事件失败: {ex.Message}");
            }
        }

        #endregion
    }
}
