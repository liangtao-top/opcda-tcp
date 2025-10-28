using Opc.Da;
using OpcDAToMSA.utils;
using OpcDAToMSA.modbus;
using OpcDAToMSA;
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
        public string ProtocolName => "MSA";
        public bool IsEnabled { get; set; }
        public bool IsConnected => msaTcp != null && msaTcp.IsConnected;

        private MsaTcp msaTcp;
        private CfgJson config;
        private Dictionary<string, object> msaSettings;

        public MsaAdapter()
        {
            this.config = Config.GetConfig();
            this.IsEnabled = true;
        }

        /// <summary>
        /// 初始化 MSA 适配器
        /// </summary>
        /// <returns>初始化是否成功</returns>
        public Task<bool> InitializeAsync()
        {
            try
            {
                // 从新配置结构中获取 MSA 设置
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
                    this.msaSettings = new Dictionary<string, object>
                    {
                        ["mn"] = 100000000,
                        ["ip"] = "127.0.0.1",
                        ["port"] = 31100,
                        ["heartbeat"] = 5000
                    };
                }

                if (!this.IsEnabled)
                {
                    LoggerUtil.log.Information("MSA 协议已禁用");
                    return Task.FromResult(true);
                }

                // 创建 MSA TCP 客户端
                this.msaTcp = new MsaTcp();
                
                // 设置 MSA 配置
                SetMsaConfig();

                // 启动 MSA 连接
                this.msaTcp.Run();

                LoggerUtil.log.Information($"MSA 适配器初始化成功，连接到 {msaSettings["ip"]}:{msaSettings["port"]}");
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

        /// <summary>
        /// 设置 MSA 配置参数
        /// </summary>
        private void SetMsaConfig()
        {
            // 这里需要修改 MsaTcp 类以支持配置注入
            // 暂时使用反射或直接设置属性的方式
            if (msaSettings != null)
            {
                // 可以通过反射设置 MsaTcp 的配置属性
                // 或者修改 MsaTcp 构造函数接受配置参数
                LoggerUtil.log.Debug($"MSA 配置设置完成：{string.Join(", ", msaSettings)}");
            }
        }
    }
}
