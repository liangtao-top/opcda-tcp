using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpcDAToMSA
{
    partial class AboutBox1 : Form
    {
        public AboutBox1()
        {
            InitializeComponent();
            this.Text = String.Format("关于 {0}", AssemblyTitle);
            this.labelProductName.Text = AssemblyProduct;
            this.labelVersion.Text = String.Format("版本 {0}", AssemblyVersion);
            this.labelCopyright.Text = AssemblyCopyright;
            //this.labelCompanyName.Text = AssemblyCompany;
            this.labelCompanyName.Text = "成都艾邦视觉文化传播有限公司";
            //this.textBoxDescription.Text = AssemblyDescription;
            this.textBoxDescription.Text = "1. 下载安装 Windows 7 Ultimate，链接：ed2k://|file|cn_windows_7_ultimate_with_sp1_x64_dvd_u_677408.iso|3420557312|B58548681854236C7939003B583A8078|/  \r\n2. Windows 7 设置用户名密码，关闭防火墙  \r\n3. 在命令行运行control userpasswords2，打开win7系统的用户账户管理，找到“要使用本机，用户必须要输入账户和密码”，点击√，去掉默认勾选，点击应用按钮，输入密码确认\r\n4. 对网卡插拔确认，并重命名标识lan1【Opc.Server】,lan2[MSA.Server]，并根据IP地址分布表设置网卡IP地址  \r\n5. 下载 OpcDAToMSA安装包 内容到D盘根目录，链接：https://pan.baidu.com/s/1P5vZkUt8f3lS5d8RV0Nl1g 提取码：c2bd  \r\n6. 按照顺序安装 windows6.1-kb4474419-v3-x64_b5614c6cea5cb4e198717789633dca16308ef79c.msu、ndp48-x86-x64-allos-enu.exe、OPC Core Components Redistributable (x86) 3.00.108.msi、npp.8.4.6.Installer.x64.exe  \r\n7. 用Notepad++打开D:\\Release\\config.json配置文件，配置MN、OPC、MSA信息  \r\n```\r\n{\r\n  \"autoStart\": true,// 开机是否自动启动\r\n  \"opcda\": {\r\n    \"host\": \"192.168.147.129\",// 远程服务器地址\r\n    \"node\": \"Matrikon.OPC.Simulation.1\",// OPC服务名称\r\n    \"username\": \"Administrator\",// 用户名\r\n    \"password\": \"123456\"// 密码\r\n  },\r\n  \"msa\": {\r\n    \"mn\": 100000000,// 设备唯一编码\r\n    \"ip\": \"10.68.45.203\",// MSA服务器地址\r\n    \"port\": 31100,// MSA服务器端口\r\n    \"interval\": 10000,// OPC数据上报至MSA服务器间隔周期，单位：毫秒\r\n    \"heartbeat\": 5000// MSA远程远程连接心跳周期，单位：毫秒\r\n  },\r\n  \"modbus\": {\r\n    \"slave\": {\r\n      \"ip\": \"0.0.0.0\",// ModbusTCP 服务监听地址\r\n      \"port\": 502,// ModbusTCP 服务监听端口\r\n      \"station\": 1// 站号 slaveId\r\n    }\r\n  },\r\n  \"registers\": { // 转发注册表，指标标签->指标编码\r\n    \"A.02GA_0729\": \"511110066004Q0003QT001\",\r\n    \"A.02GA_0730\": \"511110066004Q0004QT001\",\r\n    \"A.02PIA_0703B\": \"511110066004G0002YL001\"\r\n  },\r\n  \"logger\": {\r\n    \"level\": \"debug\",// 日记级别，Verbose，Debug，Info，Warn，Error，Fatal\r\n    \"file\": \"logs/log.txt\" // 日志文件路径，删除配置项则不记录\r\n  }\r\n}";
        }

        #region 程序集特性访问器

        public string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public string AssemblyDescription
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public string AssemblyProduct
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public string AssemblyCopyright
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public string AssemblyCompany
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }
        #endregion

        private void okButton_Click(object sender, EventArgs e)
        {
            this.Dispose(true);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            //调用系统默认的浏览器
            System.Diagnostics.Process.Start(this.linkLabel1.Text);
        }
    }
}
