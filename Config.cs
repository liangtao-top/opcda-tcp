using Newtonsoft.Json;
using OpcDAToMSA.utils;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace OpcDAToMSA
{
    class Config
    {
        private static CfgJson cfg = null;

        public static CfgJson GetConfig() {
            if (cfg != null) {
                return cfg;
            }
            string jsonfile = Path.Combine(Application.StartupPath, "config.json");
            LoggerUtil.log.Debug(jsonfile);
            if (!File.Exists(jsonfile))
            {
                string msg = "找不到配置文件：" + jsonfile;
                LoggerUtil.log.Error(msg);
            }
            string jsonStr = File.ReadAllText(jsonfile);
            cfg = JsonConvert.DeserializeObject<CfgJson>(jsonStr);
            if (cfg == null) {
                string msg = "配置文件：" + jsonfile + "，不是有效的JSON文件";
                LoggerUtil.log.Error(msg);
            }
            //LoggerUtil.log.Debug("Config: \n{@cfg}", JsonConvert.SerializeObject(cfg, new JsonSerializerSettings() { Formatting = Formatting.Indented }));
            LoggerUtil.log.Debug("Config: {@cfg}", cfg);
            return cfg;
        }

        public static CfgJson heavyLoad()
        {
            string jsonfile = Path.Combine(Application.StartupPath, "config.json");
            LoggerUtil.log.Debug(jsonfile);
            if (!File.Exists(jsonfile))
            {
                string msg = "找不到配置文件：" + jsonfile;
                LoggerUtil.log.Error(msg);
            }
            string jsonStr = File.ReadAllText(jsonfile);
            cfg = JsonConvert.DeserializeObject<CfgJson>(jsonStr);
            if (cfg == null)
            {
                string msg = "配置文件：" + jsonfile + "，不是有效的JSON文件";
                LoggerUtil.log.Error(msg);
            }
            //LoggerUtil.log.Debug("Config: \n{@cfg}", JsonConvert.SerializeObject(cfg, new JsonSerializerSettings() { Formatting = Formatting.Indented }));
            LoggerUtil.log.Debug("Config: {@cfg}", cfg);
            return cfg;
        }
    }

    class CfgJson
    {
        // 开机自动启动
        public bool AutoStart { get; set; }
        public OpcDaJson Opcda { get; set; }
        // 协议配置字典
        public Dictionary<string, ProtocolConfig> Protocols { get; set; }
        // 指标注册表 位号->编码
        public Dictionary<string, string> Registers { get; set; }
        public LoggerJson Logger { get; set; }
    }

    class LoggerJson
    {
        public string Level { get; set; }
        public string File { get; set; }

    }

    class OpcDaJson
    {
        public string Host { get; set; }
        public string Node { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Type { get; set; }
    }

    /// <summary>
    /// 协议配置基类
    /// </summary>
    class ProtocolConfig
    {
        /// <summary>
        /// 是否启用该协议
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 协议特定设置
        /// </summary>
        public Dictionary<string, object> Settings { get; set; }
    }
}
