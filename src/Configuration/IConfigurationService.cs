using OpcDAToMSA.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace OpcDAToMSA.Configuration
{
    /// <summary>
    /// 配置数据类
    /// </summary>
    public class CfgJson
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

    /// <summary>
    /// 日志配置类
    /// </summary>
    public class LoggerJson
    {
        public string Level { get; set; }
        public string File { get; set; }
    }

    /// <summary>
    /// OPC DA配置类
    /// </summary>
    public class OpcDaJson
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
    public class ProtocolConfig
    {
        /// <summary>
        /// 是否启用该协议
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 协议特定设置
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 配置验证结果
    /// </summary>
    public class ConfigurationValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// 获取错误消息（所有错误用分号分隔）
        /// </summary>
        public string ErrorMessage => string.Join("; ", Errors);

        /// <summary>
        /// 添加错误
        /// </summary>
        /// <param name="error">错误消息</param>
        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        /// <param name="warning">警告消息</param>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }
    }

    /// <summary>
    /// 配置变更事件参数
    /// </summary>
    public class ConfigurationChangedEventArgs : EventArgs
    {
        public CfgJson OldConfig { get; }
        public CfgJson NewConfig { get; }

        public ConfigurationChangedEventArgs(CfgJson oldConfig, CfgJson newConfig)
        {
            OldConfig = oldConfig;
            NewConfig = newConfig;
        }
    }

    /// <summary>
    /// 配置服务接口
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// 获取当前配置
        /// </summary>
        CfgJson GetConfiguration();

        /// <summary>
        /// 重新加载配置
        /// </summary>
        bool ReloadConfiguration();

        /// <summary>
        /// 保存配置
        /// </summary>
        bool SaveConfiguration(CfgJson configuration);

        /// <summary>
        /// 启用配置热更新
        /// </summary>
        void EnableHotReload();

        /// <summary>
        /// 禁用配置热更新
        /// </summary>
        void DisableHotReload();

        /// <summary>
        /// 配置变更事件
        /// </summary>
        event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        /// <summary>
        /// 是否启用热更新
        /// </summary>
        bool IsHotReloadEnabled { get; }
    }

    /// <summary>
    /// 配置服务实现
    /// </summary>
    public class ConfigurationService : IConfigurationService, IDisposable
    {
        #region Private Fields

        private CfgJson _currentConfiguration;
        private readonly string _configFilePath;
        private System.IO.FileSystemWatcher _configWatcher;
        private bool _isWatching = false;
        private readonly object _lock = new object();

        #endregion

        #region Events

        public event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        #endregion

        #region Properties

        public bool IsHotReloadEnabled => _isWatching;

        #endregion

        #region Constructor

        public ConfigurationService()
        {
            _configFilePath = Path.Combine(Application.StartupPath, "config", "config.json");
            LoadConfiguration();
        }

        #endregion

        #region Public Methods

        public CfgJson GetConfiguration()
        {
            lock (_lock)
            {
                return _currentConfiguration;
            }
        }

        public bool ReloadConfiguration()
        {
            try
            {
                var oldConfig = _currentConfiguration;
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

        public bool SaveConfiguration(CfgJson configuration)
        {
            try
            {
                // 验证配置
                var validationResult = ValidateConfiguration(configuration);
                if (!validationResult.IsValid)
                {
                    LoggerUtil.log.Error($"配置验证失败，无法保存：{validationResult.ErrorMessage}");
                    return false;
                }

                // 确保目录存在
                var configDir = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(configuration, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(_configFilePath, jsonContent);

                lock (_lock)
                {
                    _currentConfiguration = configuration;
                }

                LoggerUtil.log.Information("配置保存成功");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "保存配置失败");
                return false;
            }
        }

        public void EnableHotReload()
        {
            try
            {
                if (_isWatching)
                {
                    LoggerUtil.log.Warning("配置热更新已启用");
                    return;
                }

                var configDir = Path.GetDirectoryName(_configFilePath);
                _configWatcher = new System.IO.FileSystemWatcher(configDir)
                {
                    Filter = "config.json",
                    NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size
                };

                _configWatcher.Changed += OnConfigurationFileChanged;
                _configWatcher.EnableRaisingEvents = true;
                _isWatching = true;

                LoggerUtil.log.Information("配置热更新已启用");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "启用配置热更新失败");
            }
        }

        public void DisableHotReload()
        {
            try
            {
                if (_configWatcher != null)
                {
                    _configWatcher.EnableRaisingEvents = false;
                    _configWatcher.Changed -= OnConfigurationFileChanged;
                    _configWatcher.Dispose();
                    _configWatcher = null;
                }

                _isWatching = false;
                LoggerUtil.log.Information("配置热更新已禁用");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "禁用配置热更新失败");
            }
        }

        #endregion

        #region Private Methods

        private CfgJson LoadConfiguration()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    LoggerUtil.log.Error($"配置文件不存在：{_configFilePath}");
                    return CreateDefaultConfiguration();
                }

                string jsonContent = File.ReadAllText(_configFilePath);
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<CfgJson>(jsonContent);

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

                lock (_lock)
                {
                    _currentConfiguration = config;
                }

                LoggerUtil.log.Information("配置加载成功");
                return config;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "加载配置失败");
                return CreateDefaultConfiguration();
            }
        }

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

        private void OnConfigurationFileChanged(object sender, System.IO.FileSystemEventArgs e)
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

        private void OnConfigurationChanged(ConfigurationChangedEventArgs e)
        {
            ConfigurationChanged?.Invoke(this, e);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            DisableHotReload();
        }

        #endregion
    }
}
