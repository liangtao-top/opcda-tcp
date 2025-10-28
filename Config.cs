using Newtonsoft.Json;
using OpcDAToMSA.Utils;
using OpcDAToMSA.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace OpcDAToMSA
{
    /// <summary>
    /// 配置管理类（已废弃，请使用 ConfigurationManager）
    /// </summary>
    [System.Obsolete("请使用 ConfigurationManager.Instance 替代")]
    class Config
    {
        /// <summary>
        /// 获取配置（已废弃）
        /// </summary>
        /// <returns>配置对象</returns>
        [System.Obsolete("请使用 ConfigurationManager.Instance.CurrentConfig")]
        public static CfgJson GetConfig() {
            return ConfigurationManager.Instance.CurrentConfig;
        }

        /// <summary>
        /// 重新加载配置（已废弃）
        /// </summary>
        /// <returns>配置对象</returns>
        [System.Obsolete("请使用 ConfigurationManager.Instance.ReloadConfiguration()")]
        public static CfgJson heavyLoad()
        {
            ConfigurationManager.Instance.ReloadConfiguration();
            return ConfigurationManager.Instance.CurrentConfig;
        }
    }
}