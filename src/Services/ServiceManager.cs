using OpcDAToMSA.Configuration;
using OpcDAToMSA.Core;
using OpcDAToMSA.Monitoring;
using OpcDAToMSA.Protocols;
using OpcDAToMSA.Services;
using OpcDAToMSA.DependencyInjection;
using OpcDAToMSA.Utils;
using OpcDAToMSA.UI.Forms;
using System;
using System.Threading.Tasks;
using IServiceProvider = OpcDAToMSA.DependencyInjection.IServiceProvider;

namespace OpcDAToMSA.Services
{
    /// <summary>
    /// 服务管理器实现
    /// </summary>
    public class ServiceManager : IServiceManager
    {
        #region Private Fields

        private readonly IConfigurationService configurationService;
        private readonly IProtocolAdapterFactory adapterFactory;
        private IDataService dataService;
        private IMonitoringService monitoringService;

        #endregion

        #region Constructor

        public ServiceManager(IConfigurationService configurationService, IProtocolAdapterFactory adapterFactory)
        {
            this.configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            this.adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        }

        #endregion

        #region Public Methods

        public async Task<bool> StartAllServicesAsync()
        {
            try
            {
                LoggerUtil.log.Information("启动所有服务");

                // 启动监控服务
                monitoringService = MonitoringService.Instance;
                monitoringService.Start();

                // 启动数据服务
                var opcProvider = new OpcNet(configurationService);
                var protocolRouter = new ProtocolRouter(configurationService, adapterFactory);
                dataService = new OpcDataService(opcProvider, protocolRouter, configurationService, monitoringService);
                var result = await dataService.StartAsync();

                if (result)
                {
                    LoggerUtil.log.Information("所有服务启动成功");
                }
                else
                {
                    LoggerUtil.log.Error("数据服务启动失败");
                }

                return result;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "启动服务失败");
                return false;
            }
        }

        public async Task<bool> StopAllServicesAsync()
        {
            try
            {
                LoggerUtil.log.Information("停止所有服务");

                // 停止数据服务
                if (dataService != null)
                {
                    await dataService.StopAsync();
                    dataService = null;
                }

                // 停止监控服务
                if (monitoringService != null)
                {
                    monitoringService.Stop();
                    monitoringService = null;
                }

                LoggerUtil.log.Information("所有服务已停止");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "停止服务失败");
                return false;
            }
        }

        public ServiceStatus GetServiceStatus()
        {
            return new ServiceStatus
            {
                DataServiceRunning = dataService?.IsRunning ?? false,
                MonitoringServiceRunning = monitoringService?.IsRunning ?? false,
                HealthReport = monitoringService?.GetHealthReport()
            };
        }

        public bool IsRunning => dataService?.IsRunning ?? false;

        public T GetService<T>() where T : class
        {
            if (typeof(T) == typeof(IDataService) || typeof(T) == typeof(IOpcDataProvider))
            {
                return dataService as T;
            }
            if (typeof(T) == typeof(IMonitoringService))
            {
                return monitoringService as T;
            }
            return null;
        }

        #endregion
    }

    /// <summary>
    /// 服务注册器
    /// </summary>
    public static class ServiceRegistrar
    {
        /// <summary>
        /// 注册所有服务
        /// </summary>
        /// <returns>服务容器</returns>
        public static IServiceContainer RegisterServices()
        {
            var container = new ServiceContainer();

            // 注册配置服务
            container.Register<IConfigurationService, ConfigurationService>(ServiceLifetime.Singleton);

            // 注册监控服务
            container.Register<IMonitoringService, MonitoringService>(ServiceLifetime.Singleton);

            // 注册协议适配器工厂
            container.Register<IProtocolAdapterFactory, ProtocolAdapterFactory>(ServiceLifetime.Singleton);

            // 注册协议路由器
            container.Register<IProtocolRouter, ProtocolRouter>(ServiceLifetime.Singleton);

            // 注册OPC数据提供者
            container.Register<IOpcDataProvider, OpcNet>(ServiceLifetime.Singleton);

            // 注册数据服务
            container.Register<IDataService, OpcDataService>(ServiceLifetime.Singleton);

            // 注册服务管理器
            container.Register<IServiceManager, ServiceManager>(ServiceLifetime.Singleton);

            // 注册UI窗体
            container.Register<Form1>(provider => new Form1(
                provider.GetService<IServiceManager>(),
                provider.GetService<IConfigurationService>()
            ), ServiceLifetime.Transient);

            return container;
        }
    }

    /// <summary>
    /// 应用程序启动器
    /// </summary>
    public static class ApplicationBootstrapper
    {
        private static IServiceProvider _serviceProvider;

        /// <summary>
        /// 初始化应用程序
        /// </summary>
        public static void Initialize()
        {
            try
            {
                LoggerUtil.log.Information("初始化应用程序");

                // 注册服务
                var container = ServiceRegistrar.RegisterServices();
                _serviceProvider = container.BuildServiceProvider();

                // 设置服务定位器
                ServiceLocator.SetServiceProvider(_serviceProvider);

                LoggerUtil.log.Information("应用程序初始化完成");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Fatal(ex, "应用程序初始化失败");
                throw;
            }
        }

        /// <summary>
        /// 获取服务提供者
        /// </summary>
        public static IServiceProvider ServiceProvider => _serviceProvider;

        /// <summary>
        /// 启动应用程序
        /// </summary>
        public static async Task<bool> StartAsync()
        {
            try
            {
                var serviceManager = _serviceProvider.GetService<IServiceManager>();
                return await serviceManager.StartAllServicesAsync();
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Fatal(ex, "启动应用程序失败");
                return false;
            }
        }

        /// <summary>
        /// 停止应用程序
        /// </summary>
        public static async Task<bool> StopAsync()
        {
            try
            {
                var serviceManager = _serviceProvider.GetService<IServiceManager>();
                return await serviceManager.StopAllServicesAsync();
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Fatal(ex, "停止应用程序失败");
                return false;
            }
        }
    }
}
