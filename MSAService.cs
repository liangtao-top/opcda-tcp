using OPCDA2MSA.opc;
using OPCDA2MSA;
using OpcDAToMSA.utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace OpcDAToMSA
{
    partial class MSAService : ServiceBase
    {
        public MSAService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // TODO: 在此处添加代码以启动服务。
            //LoggerUtil.log.Information("Welcome OpcDaToMSA V2022.12.02");
            //LoggerUtil.Configuration(Config.GetConfig().Logger);

            //OpcNet client = new OpcNet();
            //client.Connect();
            //client.MsaTcp();
            WriteInfo("服务启动");
        }

        protected override void OnStop()
        {
            // TODO: 在此处添加代码以执行停止服务所需的关闭操作。
            WriteInfo("服务停止");
        }

        private string filePath = @"F:\ServiceLog.txt";

        private void WriteInfo(string info)
        {
            using (FileStream stream = new FileStream(filePath, FileMode.Append))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.WriteLine($"{DateTime.Now},{info}");
                }
            }
        }
    }
}
