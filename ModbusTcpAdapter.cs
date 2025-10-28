using Opc.Da;
using OpcDAToMSA.utils;
using OpcDAToMSA.modbus;
using OpcDAToMSA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpcDAToMSA.Protocols
{
    /// <summary>
    /// Modbus TCP 协议适配器
    /// </summary>
    public class ModbusTcpAdapter : IProtocolAdapter
    {
        public string ProtocolName => "Modbus TCP";
        public bool IsEnabled { get; set; }
        public bool IsConnected => modbusTcp != null && modbusTcp.IsConnected;

        private ModbusTcp modbusTcp;
        private CfgJson config;
        private Dictionary<string, object> modbusTcpSettings;
        private Dictionary<string, int> registerMapping;

        public ModbusTcpAdapter()
        {
            this.config = Config.GetConfig();
            this.IsEnabled = true;
            this.registerMapping = new Dictionary<string, int>();
        }

        /// <summary>
        /// 初始化 Modbus TCP 适配器
        /// </summary>
        /// <returns>初始化是否成功</returns>
        public Task<bool> InitializeAsync()
        {
            try
            {
                // 从配置中获取 Modbus TCP 设置
                if (config.Protocols != null && config.Protocols.ContainsKey("modbusTcp"))
                {
                    var modbusTcpConfig = config.Protocols["modbusTcp"];
                    this.IsEnabled = modbusTcpConfig.Enabled;
                    this.modbusTcpSettings = modbusTcpConfig.Settings;
                }
                else
                {
                    // 使用默认设置
                    LoggerUtil.log.Warning("未找到 Modbus TCP 协议配置，使用默认设置");
                    this.modbusTcpSettings = new Dictionary<string, object>
                    {
                        ["ip"] = "0.0.0.0",
                        ["port"] = 502,
                        ["station"] = 1
                    };
                }

                if (!this.IsEnabled)
                {
                    LoggerUtil.log.Information("Modbus TCP 协议已禁用");
                    return Task.FromResult(true);
                }

                // 解析配置参数
                var ip = modbusTcpSettings.ContainsKey("ip") ? modbusTcpSettings["ip"].ToString() : "0.0.0.0";
                var port = modbusTcpSettings.ContainsKey("port") ? Convert.ToInt32(modbusTcpSettings["port"]) : 502;
                var station = modbusTcpSettings.ContainsKey("station") ? Convert.ToByte(modbusTcpSettings["station"]) : (byte)1;

                // 创建 Modbus TCP 服务端
                this.modbusTcp = new ModbusTcp(ip, port, station);
                
                // 设置寄存器映射
                SetupRegisterMapping();

                // 启动 Modbus TCP 服务
                this.modbusTcp.Run();

                LoggerUtil.log.Information($"Modbus TCP 适配器初始化成功，监听 {ip}:{port}，站号：{station}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "Modbus TCP 适配器初始化失败");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 发送数据到 Modbus TCP 寄存器
        /// </summary>
        /// <param name="data">OPC DA 数据</param>
        /// <returns>发送是否成功</returns>
        public Task<bool> SendDataAsync(ItemValueResult[] data)
        {
            try
            {
                if (!this.IsEnabled || this.modbusTcp == null)
                {
                    return Task.FromResult(false);
                }

                // 批量更新 Modbus TCP 寄存器
                int successCount = this.modbusTcp.UpdateRegisters(data);
                
                LoggerUtil.log.Debug($"Modbus TCP 数据更新完成，成功：{successCount}/{data.Length}");
                return Task.FromResult(successCount > 0);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "Modbus TCP 数据更新失败");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 断开 Modbus TCP 连接
        /// </summary>
        /// <returns>断开是否成功</returns>
        public Task<bool> DisconnectAsync()
        {
            try
            {
                if (this.modbusTcp != null)
                {
                    this.modbusTcp.Stop();
                    this.modbusTcp = null;
                }

                LoggerUtil.log.Information("Modbus TCP 适配器已停止");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "Modbus TCP 适配器停止失败");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 设置寄存器映射
        /// </summary>
        private void SetupRegisterMapping()
        {
            try
            {
                // 从配置中获取寄存器映射
                if (modbusTcpSettings.ContainsKey("registerMapping") && 
                    modbusTcpSettings["registerMapping"] is Dictionary<string, object> mappingConfig)
                {
                    foreach (var kvp in mappingConfig)
                    {
                        if (int.TryParse(kvp.Value.ToString(), out int address))
                        {
                            registerMapping[kvp.Key] = address;
                        }
                    }
                }
                else
                {
                    // 使用默认映射：OPC标签名 -> 寄存器地址
                    // 这里可以根据实际需求调整映射策略
                    int baseAddress = 1000; // 起始地址
                    int addressIndex = 0;
                    
                    foreach (var register in config.Registers)
                    {
                        registerMapping[register.Key] = baseAddress + addressIndex;
                        addressIndex++;
                    }
                }

                // 设置到 ModbusTcp 实例
                if (modbusTcp != null)
                {
                    modbusTcp.SetRegisterMapping(registerMapping);
                }

                LoggerUtil.log.Information($"Modbus TCP 寄存器映射设置完成，共 {registerMapping.Count} 个映射");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "设置 Modbus TCP 寄存器映射失败");
            }
        }

        /// <summary>
        /// 获取寄存器映射信息
        /// </summary>
        /// <returns>寄存器映射字典</returns>
        public Dictionary<string, int> GetRegisterMapping()
        {
            return new Dictionary<string, int>(registerMapping);
        }

        /// <summary>
        /// 添加单个寄存器映射
        /// </summary>
        /// <param name="tagName">OPC标签名</param>
        /// <param name="address">寄存器地址</param>
        public void AddRegisterMapping(string tagName, int address)
        {
            registerMapping[tagName] = address;
            if (modbusTcp != null)
            {
                modbusTcp.SetRegisterMapping(registerMapping);
            }
        }
    }
}
