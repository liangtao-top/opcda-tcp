using OpcXml.Da;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OPCDA2MSA;
using System.Net.Http;
using System.Windows.Forms;
using System.Reflection;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using Opc.Da;

namespace OpcDAToMSA.modbus
{
    class MsaTcp
    {

        private readonly CfgJson cfg = Config.GetConfig();

        public void Run() {
            var cfg = Config.GetConfig();

            //实例化Socket
            var tcpClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EndPoint ep = new IPEndPoint(IPAddress.Parse(cfg.Msa.Ip), cfg.Msa.Port);
            try
            {
                tcpClient.Connect(ep);
                Console.WriteLine($@"MSA Server {cfg.Msa.Ip}:{cfg.Msa.Port} is connected");
                Task.Run(new Action(() =>
                {
                  
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void Send(ItemValueResult[] values) {
            int[] regs = cfg.Modbus.Slave.Registers.ToArray();

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                {
                    int index = regs[i];
                    System.Type type = values[i].Value.GetType();
                    //Console.WriteLine(type);
                    Console.WriteLine($@"{values[i].ItemName}@{index}={values[i].Value}");
                    //Console.WriteLine(JsonConvert.SerializeObject(ConvertUtil.ObjectToBytes(values[i].Value)));

                }
            }
        }

    }
}
