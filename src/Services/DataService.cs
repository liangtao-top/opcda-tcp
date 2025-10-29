using OpcDAToMSA.Core;
using OpcDAToMSA.Protocols;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Monitoring;
using OpcDAToMSA.Utils;
using Opc.Da;
using System;
using System.Linq;
using System.Threading;
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
        private CancellationTokenSource collectionCancellationTokenSource;
        private Task collectionTask;

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
                
                // 启动定时采集循环
                StartDataCollectionLoop();
                
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

                // 停止定时采集循环
                StopDataCollectionLoop();

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

        #region Private Methods

        /// <summary>
        /// 启动数据采集循环
        /// </summary>
        private void StartDataCollectionLoop()
        {
            try
            {
                var config = configurationService.GetConfiguration();
                int interval = config.Opcda?.Interval ?? 3000;

                // 取消之前的循环（如果存在）
                StopDataCollectionLoop();

                // 创建新的取消令牌
                collectionCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = collectionCancellationTokenSource.Token;

                // 启动采集任务
                collectionTask = Task.Run(async () =>
                {
                    LoggerUtil.log.Information($"开始定时采集循环，间隔：{interval}ms");
                    
                    while (isRunning && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            // 检查连接状态
                            if (!opcProvider.IsConnected)
                            {
                                LoggerUtil.log.Warning("OPC DA 未连接，跳过本次采集");
                                await Task.Delay(interval, cancellationToken);
                                continue;
                            }

                            // 读取数据
                            var data = await ReadDataAsync();
                            
                            if (data != null && data.Length > 0)
                            {
                                // 发送数据到协议路由器（转发到MQTT等）
                                await SendDataAsync(data);
                            }
                            else
                            {
                                LoggerUtil.log.Debug("本次采集未获取到数据");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // 正常取消，退出循环
                            break;
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.log.Error(ex, "采集循环执行异常，尝试重连");
                            
                            // 如果连接断开，尝试重连
                            if (!opcProvider.IsConnected)
                            {
                                try
                                {
                                    LoggerUtil.log.Information("尝试重新连接OPC DA服务器...");
                                    await opcProvider.ConnectAsync();
                                }
                                catch (Exception reconnectEx)
                                {
                                    LoggerUtil.log.Error(reconnectEx, "重连失败");
                                }
                            }
                        }

                        // 等待指定的间隔时间
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(interval, cancellationToken);
                        }
                    }
                    
                    LoggerUtil.log.Information("定时采集循环已停止");
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "启动数据采集循环失败");
            }
        }

        /// <summary>
        /// 停止数据采集循环
        /// </summary>
        private void StopDataCollectionLoop()
        {
            try
            {
                // 取消循环
                if (collectionCancellationTokenSource != null)
                {
                    collectionCancellationTokenSource.Cancel();
                    collectionCancellationTokenSource.Dispose();
                    collectionCancellationTokenSource = null;
                }

                // 等待任务完成
                if (collectionTask != null)
                {
                    try
                    {
                        collectionTask.Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (AggregateException ex)
                    {
                        // 检查是否只是取消异常，如果是则正常
                        if (ex.InnerExceptions.All(e => e is TaskCanceledException))
                        {
                            LoggerUtil.log.Debug("采集循环已正常取消");
                        }
                        else
                        {
                            LoggerUtil.log.Warning(ex, "等待采集循环停止时发生异常");
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        LoggerUtil.log.Debug("采集循环已正常取消");
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.log.Warning(ex, "等待采集循环停止超时");
                    }
                    collectionTask = null;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "停止数据采集循环失败");
            }
        }

        #endregion
    }
}