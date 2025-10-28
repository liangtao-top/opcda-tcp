using Opc.Da;
using OpcDAToMSA.utils;
using OPCDA2MSA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpcDAToMSA.Protocols
{
    /// <summary>
    /// 协议路由器，负责管理多个协议适配器并分发数据
    /// </summary>
    public class ProtocolRouter
    {
        private readonly List<IProtocolAdapter> adapters;
        private readonly CfgJson config;
        private bool isInitialized = false;

        public ProtocolRouter()
        {
            this.config = Config.GetConfig();
            this.adapters = new List<IProtocolAdapter>();
        }

        /// <summary>
        /// 初始化所有启用的协议适配器
        /// </summary>
        /// <returns>初始化是否成功</returns>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                LoggerUtil.log.Information("开始初始化协议路由器");

                // 初始化 MSA 适配器
                if (config.Protocols.ContainsKey("msa") && config.Protocols["msa"].Enabled)
                {
                    var msaAdapter = new MsaAdapter();
                    if (await msaAdapter.InitializeAsync())
                    {
                        adapters.Add(msaAdapter);
                        LoggerUtil.log.Information("MSA 适配器初始化成功");
                    }
                    else
                    {
                        LoggerUtil.log.Error("MSA 适配器初始化失败");
                    }
                }

                // 初始化 MQTT 适配器
                if (config.Protocols.ContainsKey("mqtt") && config.Protocols["mqtt"].Enabled)
                {
                    var mqttAdapter = new MqttAdapter();
                    if (await mqttAdapter.InitializeAsync())
                    {
                        adapters.Add(mqttAdapter);
                        LoggerUtil.log.Information("MQTT 适配器初始化成功");
                    }
                    else
                    {
                        LoggerUtil.log.Error("MQTT 适配器初始化失败");
                    }
                }

                // 初始化 Modbus TCP 适配器
                if (config.Protocols.ContainsKey("modbusTcp") && config.Protocols["modbusTcp"].Enabled)
                {
                    var modbusTcpAdapter = new ModbusTcpAdapter();
                    if (await modbusTcpAdapter.InitializeAsync())
                    {
                        adapters.Add(modbusTcpAdapter);
                        LoggerUtil.log.Information("Modbus TCP 适配器初始化成功");
                    }
                    else
                    {
                        LoggerUtil.log.Error("Modbus TCP 适配器初始化失败");
                    }
                }

                isInitialized = true;
                LoggerUtil.log.Information($"协议路由器初始化完成，共加载 {adapters.Count} 个适配器");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Fatal(ex, "协议路由器初始化失败");
                return false;
            }
        }

        /// <summary>
        /// 向所有启用的协议适配器发送数据
        /// </summary>
        /// <param name="data">OPC DA 数据</param>
        /// <returns>发送是否成功</returns>
        public async Task<bool> SendDataAsync(ItemValueResult[] data)
        {
            if (!isInitialized)
            {
                LoggerUtil.log.Warning("协议路由器未初始化");
                return false;
            }

            if (data == null || data.Length == 0)
            {
                LoggerUtil.log.Warning("数据为空，跳过发送");
                return true;
            }

            var tasks = new List<Task<bool>>();
            var enabledAdapters = adapters.Where(a => a.IsEnabled && a.IsConnected).ToList();

            if (enabledAdapters.Count == 0)
            {
                LoggerUtil.log.Warning("没有启用的协议适配器");
                return false;
            }

            // 并行发送数据到所有启用的适配器
            foreach (var adapter in enabledAdapters)
            {
                tasks.Add(SendToAdapter(adapter, data));
            }

            try
            {
                var results = await Task.WhenAll(tasks);
                var successCount = results.Count(r => r);
                var totalCount = results.Length;

                LoggerUtil.log.Debug($"数据发送完成：成功 {successCount}/{totalCount}");

                return successCount > 0; // 至少有一个成功就算成功
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "数据发送过程中发生异常");
                return false;
            }
        }

        /// <summary>
        /// 向单个适配器发送数据
        /// </summary>
        /// <param name="adapter">协议适配器</param>
        /// <param name="data">数据</param>
        /// <returns>发送结果</returns>
        private async Task<bool> SendToAdapter(IProtocolAdapter adapter, ItemValueResult[] data)
        {
            try
            {
                var result = await adapter.SendDataAsync(data);
                if (!result)
                {
                    LoggerUtil.log.Warning($"协议 {adapter.ProtocolName} 数据发送失败");
                }
                return result;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, $"协议 {adapter.ProtocolName} 数据发送异常");
                return false;
            }
        }

        /// <summary>
        /// 停止所有协议适配器
        /// </summary>
        /// <returns>停止是否成功</returns>
        public async Task<bool> StopAsync()
        {
            try
            {
                LoggerUtil.log.Information("开始停止协议路由器");

                var tasks = adapters.Select(adapter => adapter.DisconnectAsync()).ToArray();
                await Task.WhenAll(tasks);

                adapters.Clear();
                isInitialized = false;

                LoggerUtil.log.Information("协议路由器已停止");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "停止协议路由器时发生异常");
                return false;
            }
        }

        /// <summary>
        /// 获取当前启用的协议列表
        /// </summary>
        /// <returns>协议名称列表</returns>
        public List<string> GetEnabledProtocols()
        {
            return adapters.Where(a => a.IsEnabled).Select(a => a.ProtocolName).ToList();
        }

        /// <summary>
        /// 获取协议连接状态
        /// </summary>
        /// <returns>协议状态字典</returns>
        public Dictionary<string, bool> GetProtocolStatus()
        {
            return adapters.ToDictionary(a => a.ProtocolName, a => a.IsConnected);
        }
    }
}
