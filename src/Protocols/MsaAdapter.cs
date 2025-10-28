using Opc.Da;
using OpcDAToMSA.Utils;
using OpcDAToMSA.Core;
using OpcDAToMSA.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpcDAToMSA.Protocols
{
    /// <summary>
    /// MSA 协议适配器
    /// </summary>
    public class MsaAdapter : IProtocolAdapter
    {
        #region Properties

        /// <summary>
        /// 协议名称
        /// </summary>
        public string ProtocolName => "MSA";

        /// <summary>
        /// 是否启用该协议
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public bool IsConnected => msaTcp != null && msaTcp.IsConnected;

        #endregion

        #region Private Fields

        private MsaTcp msaTcp;
        private readonly IConfigurationService configurationService;
        private Dictionary<string, object> msaSettings;

        #endregion

        #region Constructor

        /// <summary>
        /// 初始化 MSA 适配器
        /// </summary>
        public MsaAdapter(IConfigurationService configurationService)
        {
            this.configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            this.IsEnabled = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 初始化 MSA 适配器
        /// </summary>
        /// <returns>初始化是否成功</returns>
        public Task<bool> InitializeAsync()
        {
            try
            {
                // 从配置中获取 MSA 设置
                var config = configurationService.GetConfiguration();
                if (config.Protocols != null && config.Protocols.ContainsKey("msa"))
                {
                    var msaConfig = config.Protocols["msa"];
                    this.IsEnabled = msaConfig.Enabled;
                    this.msaSettings = msaConfig.Settings;
                }
                else
                {
                    // 使用默认设置
                    LoggerUtil.log.Warning("未找到 MSA 协议配置，使用默认设置");
                    this.msaSettings = GetDefaultMsaSettings();
                }

                if (!this.IsEnabled)
                {
                    LoggerUtil.log.Information("MSA 协议已禁用");
                    return Task.FromResult(true);
                }

                // 创建 MSA TCP 客户端
                this.msaTcp = new MsaTcp(configurationService);

                // 启动 MSA 连接
                this.msaTcp.Run();

                var ip = msaSettings.ContainsKey("ip") ? msaSettings["ip"].ToString() : "127.0.0.1";
                var port = msaSettings.ContainsKey("port") ? msaSettings["port"].ToString() : "31100";
                LoggerUtil.log.Information($"MSA 适配器初始化成功，连接到 {ip}:{port}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "MSA 适配器初始化失败");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 发送数据到 MSA 服务器
        /// </summary>
        /// <param name="data">OPC DA 数据</param>
        /// <returns>发送是否成功</returns>
        public Task<bool> SendDataAsync(ItemValueResult[] data)
        {
            try
            {
                if (!this.IsEnabled || this.msaTcp == null)
                {
                    return Task.FromResult(false);
                }

                // 调用原有的 MSA 发送逻辑
                this.msaTcp.Send(data);

                LoggerUtil.log.Debug($"MSA 数据发送成功，共 {data.Length} 个数据点");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "MSA 数据发送失败");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 断开 MSA 连接
        /// </summary>
        /// <returns>断开是否成功</returns>
        public Task<bool> DisconnectAsync()
        {
            try
            {
                if (this.msaTcp != null)
                {
                    this.msaTcp.Stop();
                    this.msaTcp = null;
                }

                LoggerUtil.log.Information("MSA 适配器已断开连接");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "MSA 适配器断开连接失败");
                return Task.FromResult(false);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 获取默认 MSA 设置
        /// </summary>
        /// <returns>默认设置字典</returns>
        private Dictionary<string, object> GetDefaultMsaSettings()
        {
            return new Dictionary<string, object>
            {
                ["mn"] = 100000000,
                ["ip"] = "127.0.0.1",
                ["port"] = 31100,
                ["heartbeat"] = 5000
            };
        }

        #endregion
    }
}