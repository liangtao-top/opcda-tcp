using Modbus.Data;
using Modbus.Device;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Windows.Forms.AxHost;
using OPCDA2MSA;

namespace OpcDAToMSA.modbus
{
    class ModbusTcp
    {

        private CfgJson cfg = null;

        private ModbusTcpSlave slave = null;

        private DataStore store = null;

        public DataStore Store { get => store; set => store = value; }

        public void Run()
        {
            cfg = Config.GetConfig();
            int[] regs = cfg.Modbus.Slave.Registers.ToArray();

            TcpListener slaveTcpListener = new TcpListener(IPAddress.Parse(cfg.Modbus.Slave.Ip), cfg.Modbus.Slave.Port);
            slave = ModbusTcpSlave.CreateTcp(cfg.Modbus.Slave.Id, slaveTcpListener);
            slave.DataStore = store = DataStoreFactory.CreateDefaultDataStore();

            //订阅数据到达事件，可以在此事件中读取寄存器
            slave.DataStore.DataStoreWrittenTo += new EventHandler<DataStoreEventArgs>((obj, o) =>
            {

            });

            //此事件，待补充
            slave.ModbusSlaveRequestReceived += new EventHandler<ModbusSlaveRequestEventArgs>((obj, o) =>
            {

            });

            //此事件，待补充
            slave.WriteComplete += new EventHandler<ModbusSlaveRequestEventArgs>((obj, o) =>
            {

            });



            (new Thread(() =>{slave.ListenAsync(); }){IsBackground = true}).Start();
        }

    }
}
