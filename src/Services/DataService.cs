using OpcDAToMSA.Core;
using OpcDAToMSA.Protocols;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Monitoring;
using OpcDAToMSA.Utils;
using Opc.Da;
using System;
using System.Threading.Tasks;

namespace OpcDAToMSA.Services
{
    /// <summary>
    /// OPC DA 数据服务实现
    /// </summary>
    public class OpcDataService : IDataService
    {
        #region Private Fields

        private readonly IOpcDataProvider opcProvider;
        private readonly IProtocolRouter protocolRouter;
        private readonly IConfigurationService configurationService;
        private readonly IMonitoringService monitoringService;
        private bool isRunning = false;

        #endregion

        #region Constructor

        public OpcDataService(IOpcDataProvider opcProvider, IProtocolRouter protocolRouter, IConfigurationService configurationService, IMonitoringService monitoringService)
        {
            this.opcProvider = opcProvider ?? throw new ArgumentNullException(nameof(opcProvider));
            this.protocolRouter = protocolRouter ?? throw new ArgumentNullException(nameof(protocolRouter));
            this.configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            this.monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
        }

        #endregion

        #region Public Properties

        public bool IsRunning => isRunning;

        /// <summary>
        /// 是否已连接（实现IDataProvider和IDataSender接口）
        /// </summary>
        public bool IsConnected => opcProvider?.IsConnected ?? false;

        /// <summary>
        /// 连接状态变更事件（实现IDataProvider和IDataSender接口）
        /// </summary>
        public event EventHandler<bool> ConnectionStatusChanged;

        /// <summary>
        /// 触发连接状态变更事件
        /// </summary>
        /// <param name="isConnected">是否已连接</param>
        protected virtual void OnConnectionStatusChanged(bool isConnected)
        {
            ConnectionStatusChanged?.Invoke(this, isConnected);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 启动服务
        /// </summary>
        /// <returns>是否成功</returns>
        public async Task<bool> StartAsync()
        {
            try
            {
                LoggerUtil.log.Information("启动 OPC DA 数据服务");

                // 初始化协议路由器
                var initResult = await protocolRouter.InitializeAsync();
                if (!initResult)
                {
                    LoggerUtil.log.Error("协议路由器初始化失败");
                    return false;
                }

                // 连接 OPC DA 服务器
                var connectResult = await opcProvider.ConnectAsync();
                if (!connectResult)
                {
                    LoggerUtil.log.Error("OPC DA 服务器连接失败");
                    return false;
                }

                // 注册健康检查
                monitoringService.RegisterHealthCheck("opcda", () => new Monitoring.HealthStatus
                {
                    ComponentName = "opcda",
                    Status = opcProvider.IsConnected ? Monitoring.HealthStatusType.Healthy : Monitoring.HealthStatusType.Unhealthy,
                    Timestamp = DateTime.Now,
                    Details = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["connected"] = opcProvider.IsConnected
                    }
                });

                isRunning = true;
                
                // 触发连接状态变更事件
                OnConnectionStatusChanged(true);
                
                LoggerUtil.log.Information("OPC DA 数据服务启动成功");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "启动 OPC DA 数据服务失败");
                return false;
            }
        }

        /// <summary>
        /// 停止服务
        /// </summary>
        /// <returns>是否成功</returns>
        public async Task<bool> StopAsync()
        {
            try
            {
                LoggerUtil.log.Information("停止 OPC DA 数据服务");

                isRunning = false;

                // 停止 OPC DA 客户端
                await opcProvider.DisconnectAsync();

                // 停止协议路由器
                await protocolRouter.StopAsync();

                // 触发连接状态变更事件
                OnConnectionStatusChanged(false);

                LoggerUtil.log.Information("OPC DA 数据服务已停止");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "停止 OPC DA 数据服务失败");
                return false;
            }
        }

        /// <summary>
        /// 读取数据
        /// </summary>
        /// <returns>数据结果</returns>
        public async Task<ItemValueResult[]> ReadDataAsync()
        {
            try
            {
                if (!isRunning || opcProvider == null)
                {
                    LoggerUtil.log.Warning("OPC DA 数据服务未运行");
                    return new ItemValueResult[0];
                }

                // 读取 OPC DA 数据
                var data = await opcProvider.ReadDataAsync();
                
                // 更新监控指标
                monitoringService.UpdateMetric("opcda_read_count", 1, "count");
                monitoringService.UpdateMetric("opcda_last_read", DateTime.Now.Ticks, "ticks");

                return data;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "读取 OPC DA 数据失败");
                monitoringService.UpdateMetric("opcda_read_errors", 1, "count");
                return new ItemValueResult[0];
            }
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns>是否成功</returns>
        public async Task<bool> SendDataAsync(ItemValueResult[] data)
        {
            try
            {
                if (!isRunning || protocolRouter == null)
                {
                    LoggerUtil.log.Warning("数据服务未运行或协议路由器未初始化");
                    return false;
                }

                if (data == null || data.Length == 0)
                {
                    LoggerUtil.log.Warning("发送数据为空");
                    return true;
                }

                var result = await protocolRouter.SendDataAsync(data);
                
                // 更新监控指标
                monitoringService.UpdateMetric("data_send_count", data.Length, "count");
                monitoringService.UpdateMetric("data_send_success", result ? 1 : 0, "count");

                return result;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "发送数据失败");
                monitoringService.UpdateMetric("data_send_errors", 1, "count");
                return false;
            }
        }

        #endregion
    }
}