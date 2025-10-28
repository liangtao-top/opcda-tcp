using Newtonsoft.Json;
using OpcDAToMSA.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace OpcDAToMSA.Configuration
{
    /// <summary>
    /// 配置管理服务
    /// </summary>
    public class ConfigurationManager
    {
        #region Private Fields

        private static ConfigurationManager instance;
        private static readonly object lockObject = new object();
        private CfgJson currentConfig;
        private readonly string configFilePath;
        private FileSystemWatcher configWatcher;
        private bool isWatching = false;

        #endregion

        #region Events

        /// <summary>
        /// 配置变更事件
        /// </summary>
        public event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        #endregion

        #region Constructor

        /// <summary>
        /// 私有构造函数，实现单例模式
        /// </summary>
        private ConfigurationManager()
        {
            configFilePath = Path.Combine(Application.StartupPath, "config", "config.json");
            LoadConfiguration();
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// 单例实例
        /// </summary>
        public static ConfigurationManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObject)
                    {
                        if (instance == null)
                        {
                            instance = new ConfigurationManager();
                        }
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// 当前配置
        /// </summary>
        public CfgJson CurrentConfig => currentConfig;

        /// <summary>
        /// 是否启用配置热更新
        /// </summary>
        public bool IsHotReloadEnabled { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// 加载配置
        /// </summary>
        /// <returns>配置对象</returns>
        public CfgJson LoadConfiguration()
        {
            try
            {
                if (!File.Exists(configFilePath))
                {
                    LoggerUtil.log.Error($"配置文件不存在：{configFilePath}");
                    return CreateDefaultConfiguration();
                }

                string jsonContent = File.ReadAllText(configFilePath);
                var config = JsonConvert.DeserializeObject<CfgJson>(jsonContent);

                if (config == null)
                {
                    LoggerUtil.log.Error("配置文件解析失败，使用默认配置");
                    return CreateDefaultConfiguration();
                }

                // 验证配置
                var validationResult = ValidateConfiguration(config);
                if (!validationResult.IsValid)
                {
                    LoggerUtil.log.Warning($"配置验证失败：{validationResult.ErrorMessage}");
                }

                currentConfig = config;
                LoggerUtil.log.Information("配置加载成功");
                return config;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "加载配置失败");
                return CreateDefaultConfiguration();
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        /// <param name="config">配置对象</param>
        /// <returns>是否保存成功</returns>
        public bool SaveConfiguration(CfgJson config)
        {
            try
            {
                // 验证配置
                var validationResult = ValidateConfiguration(config);
                if (!validationResult.IsValid)
                {
                    LoggerUtil.log.Error($"配置验证失败，无法保存：{validationResult.ErrorMessage}");
                    return false;
                }

                // 确保目录存在
                var configDir = Path.GetDirectoryName(configFilePath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                string jsonContent = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configFilePath, jsonContent);

                currentConfig = config;
                LoggerUtil.log.Information("配置保存成功");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "保存配置失败");
                return false;
            }
        }

        /// <summary>
        /// 启用配置热更新
        /// </summary>
        public void EnableHotReload()
        {
            try
            {
                if (isWatching)
                {
                    LoggerUtil.log.Warning("配置热更新已启用");
                    return;
                }

                var configDir = Path.GetDirectoryName(configFilePath);
                configWatcher = new FileSystemWatcher(configDir)
                {
                    Filter = "config.json",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };

                configWatcher.Changed += OnConfigurationFileChanged;
                configWatcher.EnableRaisingEvents = true;
                isWatching = true;
                IsHotReloadEnabled = true;

                LoggerUtil.log.Information("配置热更新已启用");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "启用配置热更新失败");
            }
        }

        /// <summary>
        /// 禁用配置热更新
        /// </summary>
        public void DisableHotReload()
        {
            try
            {
                if (configWatcher != null)
                {
                    configWatcher.EnableRaisingEvents = false;
                    configWatcher.Changed -= OnConfigurationFileChanged;
                    configWatcher.Dispose();
                    configWatcher = null;
                }

                isWatching = false;
                IsHotReloadEnabled = false;
                LoggerUtil.log.Information("配置热更新已禁用");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "禁用配置热更新失败");
            }
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        /// <returns>是否重新加载成功</returns>
        public bool ReloadConfiguration()
        {
            try
            {
                var oldConfig = currentConfig;
                var newConfig = LoadConfiguration();

                if (newConfig != null)
                {
                    OnConfigurationChanged(new ConfigurationChangedEventArgs(oldConfig, newConfig));
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "重新加载配置失败");
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认配置对象</returns>
        private CfgJson CreateDefaultConfiguration()
        {
            return new CfgJson
            {
                AutoStart = false,
                Opcda = new OpcDaJson
                {
                    Host = "localhost",
                    Node = "Matrikon.OPC.Simulation.1",
                    Type = "Everyone",
                    Username = "",
                    Password = ""
                },
                Protocols = new Dictionary<string, ProtocolConfig>
                {
                    ["msa"] = new ProtocolConfig
                    {
                        Enabled = true,
                        Settings = new Dictionary<string, object>
                        {
                            ["mn"] = 100000000,
                            ["ip"] = "127.0.0.1",
                            ["port"] = 31100,
                            ["heartbeat"] = 5000
                        }
                    }
                },
                Registers = new Dictionary<string, string>(),
                Logger = new LoggerJson
                {
                    Level = "Info",
                    File = "logs/log.txt"
                }
            };
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        /// <param name="config">配置对象</param>
        /// <returns>验证结果</returns>
        private ConfigurationValidationResult ValidateConfiguration(CfgJson config)
        {
            var result = new ConfigurationValidationResult();

            try
            {
                // 验证 OPC DA 配置
                if (config.Opcda == null)
                {
                    result.AddError("OPC DA 配置不能为空");
                }
                else
                {
                    if (string.IsNullOrEmpty(config.Opcda.Host))
                        result.AddError("OPC DA Host 不能为空");
                    if (string.IsNullOrEmpty(config.Opcda.Node))
                        result.AddError("OPC DA Node 不能为空");
                }

                // 验证协议配置
                if (config.Protocols != null)
                {
                    foreach (var protocol in config.Protocols)
                    {
                        if (protocol.Value.Settings == null)
                        {
                            result.AddError($"协议 {protocol.Key} 的设置不能为空");
                        }
                    }
                }

                // 验证日志配置
                if (config.Logger == null)
                {
                    result.AddError("日志配置不能为空");
                }

                result.IsValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                result.AddError($"配置验证异常：{ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 配置文件变更事件处理
        /// </summary>
        private void OnConfigurationFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // 延迟处理，避免文件正在写入
                System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                {
                    ReloadConfiguration();
                });
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "处理配置文件变更事件失败");
            }
        }

        /// <summary>
        /// 触发配置变更事件
        /// </summary>
        private void OnConfigurationChanged(ConfigurationChangedEventArgs e)
        {
            ConfigurationChanged?.Invoke(this, e);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            DisableHotReload();
        }

        #endregion
    }

    /// <summary>
    /// 配置变更事件参数
    /// </summary>
    public class ConfigurationChangedEventArgs : EventArgs
    {
        public CfgJson OldConfiguration { get; }
        public CfgJson NewConfiguration { get; }

        public ConfigurationChangedEventArgs(CfgJson oldConfig, CfgJson newConfig)
        {
            OldConfiguration = oldConfig;
            NewConfiguration = newConfig;
        }
    }

    /// <summary>
    /// 配置验证结果
    /// </summary>
    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; } = new List<string>();

        public string ErrorMessage => string.Join("; ", Errors);

        public void AddError(string error)
        {
            Errors.Add(error);
        }
    }
}
