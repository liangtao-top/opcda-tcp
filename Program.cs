using OpcDAToMSA.utils;
using System;
using System.Windows.Forms;
using System.ServiceProcess;//用来控制服务的启动和停止
using System.Timers;
using System.Threading;
using OpcDAToMSA.Properties;
using System.Runtime.InteropServices;
using OPCDA2MSA;
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
            LoggerUtil.Configuration(Config.GetConfig().Logger);
            AutoStart(Config.GetConfig().AutoStart);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false); 
            Application.Run(new Form1());
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
                    R_run.SetValue("OPCDA2MSA", Application.ExecutablePath);
                    R_run.Close();
                    R_local.Close();
                }
                else
                {
                    RegistryKey R_local = Registry.LocalMachine;//RegistryKey R_local = Registry.CurrentUser;
                    RegistryKey R_run = R_local.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                    R_run.DeleteValue("OPCDA2MSA", false);
                    R_run.Close();
                    R_local.Close();
                }
            }
            catch (Exception e)
            {
                LoggerUtil.log.Fatal(e, "您需要管理员权限修改");
            }
        }

    }
}
