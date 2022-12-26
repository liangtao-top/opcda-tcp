using System;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using OpcDAToMSA.utils;
using Newtonsoft.Json;
using OPCDA2MSA.opc;
using OPCDA2MSA;
using System.Security.Cryptography;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;
using System.Linq;

namespace OpcDAToMSA
{
    public partial class Form1 : Form
    {
        private MenuItem startMenuItem;
        private MenuItem stopMenuItem;
        public Form1()
        {

            InitializeComponent();

            //加载时 取消跨线程检查
            Control.CheckForIllegalCrossThreadCalls = false;

            var cm = new ContextMenu();

            startMenuItem = new MenuItem("启动", new EventHandler(delegate (object sender, EventArgs e)
            {
                this.startButton_Click(sender, e);
            }));
            stopMenuItem = new MenuItem("停止", new EventHandler(delegate (object sender, EventArgs e)
            {
                this.stopButton_Click(sender, e);
            }));
            cm.MenuItems.Add(startMenuItem);
            cm.MenuItems.Add(stopMenuItem);
            cm.MenuItems.Add("-");
            cm.MenuItems.Add(new MenuItem("关于", new EventHandler(delegate (object sender, EventArgs e)
            {
                AboutBox1 aboutBox = new AboutBox1();
                aboutBox.Show();
            })));
            cm.MenuItems.Add(new MenuItem("退出", new EventHandler(delegate (object sender, EventArgs e)
            {
                this.notifyIcon1.Dispose();
                this.Dispose(true);
                //强制结束进程并退出
                System.Diagnostics.Process.GetCurrentProcess().Kill();
                Environment.Exit(0);
            })));
            this.notifyIcon1.ContextMenu = cm;
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            this.label1.Focus();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            w = this.Width;
            h = this.Height;
            (new Thread(new ThreadStart(LoggerListen))).Start();
            if (Config.GetConfig().AutoStart)
            {
                this.startButton_Click(sender, e);
            }
        }

        private void LoggerListen()
        {
            HttpListener listener = new HttpListener();
            try
            {
                listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                listener.Prefixes.Add($"http://127.0.0.1:31137/");
                listener.Start();
            }
            catch (Exception e)
            {
                LoggerUtil.log.Fatal(e, "日志监听服务意外终止");
                DialogResult dialogResult = MessageBox.Show(e.Message, "日志监听服务", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
            LoggerUtil.log.Information($"Logger Server Listening At http://127.0.0.1:31137");
            while (true)
            {
                //等待请求连接
                //没有请求则GetContext处于阻塞状态
                HttpListenerContext ctx = listener.GetContext();
                if (ctx != null)
                {
                    Stream stream = ctx.Request.InputStream;
                    StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                    string content = reader.ReadToEnd();
                    //Console.WriteLine(content);
                    //Console.WriteLine(ctx.Request.Url.AbsolutePath.ToString());
                    if (ctx.Request.Url.AbsolutePath.ToString() == "/ui-events")
                    {
                        UiEvent body = JsonConvert.DeserializeObject<UiEvent>(content);
                        UpdateUI(body);
                    }
                    else if (ctx.Request.Url.AbsolutePath.ToString() == "/log-events")
                    {
                        LogEvent[] body = JsonConvert.DeserializeObject<LogEvent[]>(content);
                        foreach (var logEvent in body)
                        {
                            ShowContent($"{logEvent.Timestamp.ToLocalTime()} [{logEvent.Level}] {logEvent.RenderedMessage}");
                            if (!string.IsNullOrEmpty(logEvent.Exception))
                            {
                                ShowContent(logEvent.Exception);
                            }
                        }
                    }
                    ctx.Response.StatusCode = 200;//设置返回给客服端http状态代码
                    ctx.Response.Close();
                }
            }
        }

        //委托
        protected delegate void ShowContentDelegate(string content);

        private void ShowContent(string content)
        {
            int maxLine = 300;//最大显示行数
            int curLine = this.textBoxDescription.Lines.Length;
            if (curLine > maxLine)
            {
                this.textBoxDescription.Lines = this.textBoxDescription.Lines.Skip(curLine - maxLine).Take(maxLine).ToArray();
            }
            this.textBoxDescription.AppendText(content + "\r\n");
        }

        private void UpdateUI(UiEvent content)
        {
            if (content.Event == "MSA")
            {
                this.label3.Text = content.Data.ToString();
            }
            else if (content.Event == "OpcDA")
            {
                this.label4.Text = content.Data.ToString();
            }
        }

        #region   拦截Windows消息
        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_CLOSE = 0xF060;
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

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Show();
            this.Activate();
        }

        #region 控件大小随窗体大小等比例缩放
        private float w;//定义当前窗体的宽度
        private float h;//定义当前窗体的高度

        private void setControls(float newx, float newy, Control cons)
        {

        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            float newx = (this.Width) / w;
            float newy = (this.Height) / h;
            setControls(newx, newy, this);
        }

        #endregion

        private OpcNet opcNet;

        private void startButton_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            startMenuItem.Enabled = false;
            Thread thread = new Thread(new ThreadStart(delegate ()
            {
                this.opcNet = new OpcNet();
                this.opcNet.Connect();
                this.opcNet.MsaTcp();
            }));
            thread.Start();
            okButton.Enabled = true;
            stopMenuItem.Enabled = true;
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            this.opcNet?.Stop();
            okButton.Enabled = false;
            stopMenuItem.Enabled = false;
            button1.Enabled = true;
            startMenuItem.Enabled = true;
        }
    }

    public class LogEvent
    {
        public DateTime Timestamp { get; set; }

        public string Level { get; set; }

        public string RenderedMessage { get; set; }

        public string Exception { get; set; }
    }

    public class UiEvent
    {
        public string Event { get; set; }

        public string Data { get; set; }
    }
}
