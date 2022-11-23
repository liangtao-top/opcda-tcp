using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace OPCDA2MSA
{
     class Config
    {
         public static CfgJson GetConfig() {
            string jsonfile = Path.Combine(Application.StartupPath, "config.json");
            Console.WriteLine(jsonfile);
            if (!File.Exists(jsonfile))
            {
                throw new Exception("找不到配置文件：" + jsonfile);
            }
            string jsonStr = File.ReadAllText(jsonfile);
            //Console.WriteLine(jsonStr);
            CfgJson cfg = JsonConvert.DeserializeObject<CfgJson>(jsonStr);
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
    }

     class OpcDaJson
    {
        public string Host { get; set; }
        public string Server { get; set; }
        public List<string> Items { get; set; }
        public int Interval { get; set; }
    }
}
