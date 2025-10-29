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

        private void ShowContent(string content)
        {
            int maxLine = 1500;//最大显示行数
            int curLine = this.textBoxDescription.Lines.Length;
            if (curLine > maxLine)
            {
                this.textBoxDescription.Lines = this.textBoxDescription.Lines.Skip(curLine - maxLine).Take(maxLine).ToArray();
            }
            this.textBoxDescription.AppendText(content + "\r\n");
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
            if (this.Form1_ClientHeight > 0)
            {
                this.textBoxDescription.Size = new Size(this.ClientSize.Width, (int)(this.ClientSize.Height - 40));
            }
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

        private void StopButton_Click(object sender, EventArgs e)
        {
            // 使用新的服务管理器停止服务
            // 停止服务（使用新的服务管理器）
            if (serviceManager != null)
            {
                serviceManager.StopAllServicesAsync().Wait();
            }
            
            okButton.Enabled = false;
            stopMenuItem.Enabled = false;
            button1.Enabled = true;
            startMenuItem.Enabled = true;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
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

                LoggerUtil.log.Information($"MSA连接状态变化: {e.IsConnected} - {e.Message}");
                
                // 可以在这里添加MSA状态相关的UI更新
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
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "处理服务状态变化事件失败");
            }
        }

        /// <summary>
        /// 系统指标更新事件处理
        /// </summary>
        private void OnMetricsUpdated(object sender, MetricsUpdatedEventArgs e)
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
