using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using OpcDAToMSA.Utils;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpcDAToMSA
{
    partial class AboutBox1 : Form
    {
        public AboutBox1()
        {
            InitializeComponent();
            this.Text = VersionManager.GenerateAboutTitle();
            this.labelProductName.Text = VersionManager.APPLICATION_TITLE;
            this.labelVersion.Text = String.Format("版本 {0}", VersionManager.DisplayVersion);
            this.labelCopyright.Text = AssemblyCopyright;
            this.labelCompanyName.Text = AssemblyCompany;
            //this.textBoxDescription.Text = AssemblyDescription;
            this.textBoxDescription.Text = "🚀 OPC DA 企业级数据网关 " + VersionManager.DisplayVersion + "\r\n\r\n" +
                "📋 产品概述\r\n" +
                "OPC DA 企业级数据网关是一款专业的工业数据采集与转发平台，支持OPC DA 2.0协议，\r\n" +
                "提供稳定可靠的数据采集、实时监控和协议转换功能。\r\n\r\n" +
                "✨ 核心特性\r\n" +
                "• 🔗 支持OPC DA 2.0标准协议\r\n" +
                "• 🌐 多协议支持：MSA TCP、MQTT、Modbus TCP\r\n" +
                "• 📊 实时数据监控与健康检查\r\n" +
                "• 🔒 企业级安全认证机制\r\n" +
                "• 📈 高性能数据转发引擎\r\n" +
                "• 🛡️ 异常恢复与故障自愈\r\n" +
                "• 📝 完整的日志记录与审计\r\n\r\n" +
                "🏢 技术架构\r\n" +
                "• 基于 .NET Framework 4.8\r\n" +
                "• 采用依赖注入设计模式\r\n" +
                "• 支持多线程并发处理\r\n" +
                "• 模块化插件架构\r\n\r\n" +
                "📞 技术支持\r\n" +
                "专业工业自动化解决方案提供商\r\n\r\n" +
                "📧 邮箱: liangtao.top@foxmail.com\r\n" +
                "📱 电话: 17380052002\r\n\r\n" +
                "© 2025 版权所有";
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
