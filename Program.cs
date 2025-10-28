using OpcDAToMSA.utils;
using System;
using System.Windows.Forms;
using OpcDAToMSA;
using Microsoft.Win32;

namespace OpcDAToMSA
{

    internal static class Program
    {

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            LoggerUtil.log.Information("Welcome OpcDaToMSA V2022.12.02");
            /** 
             * 当前用户是管理员的时候，直接启动应用程序 
             * 如果不是管理员，则使用启动对象启动程序，以确保使用管理员身份运行 
            */
            //获得当前登录的Windows用户标示 
            System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            LoggerUtil.log.Information("isAdmin: {@isAdmin}", isAdmin);
            //判断当前登录用户是否为管理员 
            if (isAdmin)  
            {
                //如果是管理员，则直接运行 
                LoggerUtil.Configuration(Config.GetConfig().Logger);
                AutoStart(Config.GetConfig().AutoStart);
                //创建Windows用户主题 
                Application.EnableVisualStyles();
                //在应用程序范围内设置控件显示文本的默认方式(可以设为使用新的GDI+ , 还是旧的GDI)
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
            else
            {
                //创建启动对象 
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                //设置运行文件 
                startInfo.FileName = Application.ExecutablePath;
                //设置启动动作,确保以管理员身份运行 
                startInfo.Verb = "runas";
                //如果不是管理员，则启动UAC 
                System.Diagnostics.Process.Start(startInfo);
                LoggerUtil.log.Information("Process UAC Start: {@startInfo}", startInfo);
                //退出 
                Application.Exit();
                LoggerUtil.log.Information("Application Exit");
            }
        }

        /// <summary>
        /// 修改程序在注册表中的键值，实现开机自启
        /// </summary>
        /// <param name="isAuto">true:开机启动,false:不开机自启</param>
        static void AutoStart(bool isAuto)
        {
            try
            {
                if (isAuto == true)
                {
                    RegistryKey R_local = Registry.LocalMachine;//RegistryKey R_local = Registry.CurrentUser;
                    RegistryKey R_run = R_local.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                    R_run.SetValue("OpcDAToMSA", Application.ExecutablePath);
                    R_run.Close();
                    R_local.Close();
                }
                else
                {
                    RegistryKey R_local = Registry.LocalMachine;//RegistryKey R_local = Registry.CurrentUser;
                    RegistryKey R_run = R_local.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                    R_run.DeleteValue("OpcDAToMSA", false);
                    R_run.Close();
                    R_local.Close();
                }
            }
            catch (Exception e)
            {
                LoggerUtil.log.Fatal(e, "开机自启，您需要管理员权限运行");
            }
        }

    }
}
