using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace OPCDA2MSA
{
    class Config
    {
        private static CfgJson cfg = null;

        public static CfgJson GetConfig() {
            if (cfg != null) {
                return cfg;
            }
            string jsonfile = Path.Combine(Application.StartupPath, "config.json");
            Console.WriteLine(jsonfile);
            if (!File.Exists(jsonfile))
            {
                throw new Exception("找不到配置文件：" + jsonfile);
            }
            string jsonStr = File.ReadAllText(jsonfile);
            //Console.WriteLine(jsonStr);
            cfg = JsonConvert.DeserializeObject<CfgJson>(jsonStr);
            if (cfg == null) {
                throw new Exception("配置文件：" + jsonfile+ "，不是有效的JSON文件");
            }
            Console.WriteLine(JsonConvert.SerializeObject(cfg));
            return cfg;
        }
    }

    class CfgJson
    {
        public OpcDaJson Opcda { get; set; }
        public ModbusJson Modbus { get; set; }

        public MsaJson Msa { get; set; }
    }

     class OpcDaJson
    {
        public string Host { get; set; }
        public string Server { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public List<string> Items { get; set; }
        public int Interval { get; set; }
    }

    class ModbusJson
    {
        public ModbusSlaveJson Slave { get; set; }
    }

    class ModbusSlaveJson
    {
        public string Ip { get; set; }
        public int Port { get; set; }
        public byte Id { get; set; }
        public List<int> Registers { get; set; }
    }

    class MsaJson
    {
        public string Ip { get; set; }
        public int Port { get; set; }

        public int Interval { get; set; }
    }
}
