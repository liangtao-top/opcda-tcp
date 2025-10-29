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
        
        /// <summary>
        /// 监控配置
        /// </summary>
        public MonitoringJson Monitoring { get; set; }
    }

    /// <summary>
    /// 日志配置类
    /// </summary>
    public class LoggerJson
    {
        /// <summary>
        /// 日志级别 (trace, debug, info, warn, error, fatal)
        /// </summary>
        public string Level { get; set; } = "info";

        /// <summary>
        /// 日志文件路径配置
        /// </summary>
        public LogPathConfig Path { get; set; } = new LogPathConfig();

        /// <summary>
        /// 最大文件大小 (如: 10MB, 100MB)
        /// </summary>
        public string MaxSize { get; set; } = "10MB";

        /// <summary>
        /// 最大保留文件数量
        /// </summary>
        public int MaxFiles { get; set; } = 5;

        /// <summary>
        /// 是否输出到控制台
        /// </summary>
        public bool Console { get; set; } = true;

        /// <summary>
        /// 是否使用结构化日志
        /// </summary>
        public bool Structured { get; set; } = true;

        /// <summary>
        /// 日志模板
        /// </summary>
        public string Template { get; set; } = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// 是否启用文件日志
        /// </summary>
        public bool FileEnabled { get; set; } = true;

        /// <summary>
        /// 异步日志缓冲区大小
        /// </summary>
        public int BufferSize { get; set; } = 1000;
    }

    /// <summary>
    /// 日志路径配置类
    /// </summary>
    public class LogPathConfig
    {
        /// <summary>
        /// 日志根目录
        /// </summary>
        public string RootDirectory { get; set; } = "logs";

        /// <summary>
        /// 主日志文件名
        /// </summary>
        public string MainFileName { get; set; } = "application.log";

        /// <summary>
        /// 错误日志文件名
        /// </summary>
        public string ErrorFileName { get; set; } = "error.log";

        /// <summary>
        /// 调试日志文件名
        /// </summary>
        public string DebugFileName { get; set; } = "debug.log";

        /// <summary>
        /// 文件名模式 (支持日期格式: yyyy-MM-dd, yyyy-MM, yyyy)
        /// </summary>
        public string FileNamePattern { get; set; } = "yyyy-MM-dd";

        /// <summary>
        /// 是否按日期分目录
        /// </summary>
        public bool UseDateSubDirectory { get; set; } = true;

        /// <summary>
        /// 是否按日志级别分文件
        /// </summary>
        public bool UseLevelSubDirectory { get; set; } = false;

        /// <summary>
        /// 获取完整日志文件路径
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        /// <returns>完整路径</returns>
        public string GetFullPath(string logLevel = "info")
        {
            var basePath = RootDirectory;

            // 按日期分目录
            if (UseDateSubDirectory)
            {
                basePath = Path.Combine(basePath, DateTime.Now.ToString(FileNamePattern));
            }

            // 按级别分目录
            if (UseLevelSubDirectory)
            {
                basePath = Path.Combine(basePath, logLevel.ToLower());
            }

            // 确保目录存在
            Directory.CreateDirectory(basePath);

            // 选择文件名
            string fileName;
            switch (logLevel.ToLower())
            {
                case "error":
                case "fatal":
                    fileName = ErrorFileName;
                    break;
                case "debug":
                case "trace":
                    fileName = DebugFileName;
                    break;
                default:
                    fileName = MainFileName;
                    break;
            }

            return Path.Combine(basePath, fileName);
        }
    }

    /// <summary>
    /// 监控配置类
    /// </summary>
    public class MonitoringJson
    {
        /// <summary>
        /// 是否启用监控
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 健康检查间隔 (毫秒)
        /// </summary>
        public int HealthCheckInterval { get; set; } = 30000;

        /// <summary>
        /// 指标收集间隔 (毫秒)
        /// </summary>
        public int MetricsInterval { get; set; } = 10000;

        /// <summary>
        /// 告警配置
        /// </summary>
        public AlertConfig Alerts { get; set; } = new AlertConfig();

        /// <summary>
        /// 性能阈值配置
        /// </summary>
        public PerformanceThresholds Performance { get; set; } = new PerformanceThresholds();
    }

    /// <summary>
    /// 告警配置类
    /// </summary>
    public class AlertConfig
    {
        /// <summary>
        /// 连接超时时间 (毫秒)
        /// </summary>
        public int ConnectionTimeout { get; set; } = 5000;

        /// <summary>
        /// 数据丢失阈值 (连续失败次数)
        /// </summary>
        public int DataLossThreshold { get; set; } = 10;

        /// <summary>
        /// 内存使用率告警阈值 (百分比)
        /// </summary>
        public double MemoryUsageThreshold { get; set; } = 80.0;

        /// <summary>
        /// CPU使用率告警阈值 (百分比)
        /// </summary>
        public double CpuUsageThreshold { get; set; } = 80.0;

        /// <summary>
        /// 是否启用邮件告警
        /// </summary>
        public bool EmailAlerts { get; set; } = false;

        /// <summary>
        /// 邮件配置
        /// </summary>
        public EmailConfig Email { get; set; } = new EmailConfig();
    }

    /// <summary>
    /// 邮件配置类
    /// </summary>
    public class EmailConfig
    {
        public string SmtpServer { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromAddress { get; set; } = "";
        public List<string> ToAddresses { get; set; } = new List<string>();
    }

    /// <summary>
    /// 性能阈值配置类
    /// </summary>
    public class PerformanceThresholds
    {
        /// <summary>
        /// 最大响应时间 (毫秒)
        /// </summary>
        public int MaxResponseTime { get; set; } = 1000;

        /// <summary>
        /// 最大数据处理延迟 (毫秒)
        /// </summary>
        public int MaxDataProcessingDelay { get; set; } = 500;

        /// <summary>
        /// 最小吞吐量 (每秒处理数量)
        /// </summary>
        public int MinThroughput { get; set; } = 10;
    }

    /// <summary>
    /// OPC DA配置类
    /// </summary>
    public class OpcDaJson
    {
        /// <summary>
        /// OPC服务器主机地址
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// OPC服务器节点名称
        /// </summary>
        public string Node { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 认证类型 (Everyone, Windows, User)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 数据采集间隔（毫秒）
        /// </summary>
        public int Interval { get; set; } = 3000;
    }

    /// <summary>
    /// 协议配置基类
    /// </summary>
    public class ProtocolConfig
    {
        /// <summary>
        /// 是否启用该协议
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 协议特定设置
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// 协议名称
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// 协议描述
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 协议版本
        /// </summary>
        public string Version { get; set; } = "1.0";
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

                // 应用日志配置
                if (config.Logger != null)
                {
                    LoggerUtil.Configuration(config.Logger);
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
                    Password = "",
                    Interval = 3000
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
                    Level = "info",
                    Path = new LogPathConfig
                    {
                        RootDirectory = "logs",
                        MainFileName = "application.log",
                        ErrorFileName = "error.log",
                        DebugFileName = "debug.log",
                        FileNamePattern = "yyyy-MM-dd",
                        UseDateSubDirectory = true,
                        UseLevelSubDirectory = false
                    },
                    MaxSize = "10MB",
                    MaxFiles = 5,
                    Console = true,
                    Structured = true,
                    Template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    FileEnabled = true,
                    BufferSize = 1000
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
