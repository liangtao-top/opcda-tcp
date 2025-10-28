using System;
using System.Threading.Tasks;
using OpcDAToMSA.DependencyInjection;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Services;
using OpcDAToMSA.Protocols;
using OpcDAToMSA.Core;
using OpcDAToMSA.Monitoring;
using OpcDAToMSA.Tests.Unit;
using Opc.Da;
using Xunit;

namespace OpcDAToMSA.Tests.Integration
{
    /// <summary>
    /// 依赖注入集成测试
    /// </summary>
    public class DependencyInjectionIntegrationTests : TestBase
    {
        [Fact]
        public void ServiceRegistrar_RegisterServices_ShouldRegisterAllServices()
        {
            // Arrange & Act
            var container = ServiceRegistrar.RegisterServices();

            // Assert
            Assert.NotNull(container);

            // 验证所有服务都能正确解析
            var configService = container.GetService<IConfigurationService>();
            var monitoringService = container.GetService<IMonitoringService>();
            var adapterFactory = container.GetService<IProtocolAdapterFactory>();
            var protocolRouter = container.GetService<IProtocolRouter>();
            var opcProvider = container.GetService<IOpcDataProvider>();
            var dataService = container.GetService<IDataService>();
            var serviceManager = container.GetService<IServiceManager>();

            Assert.NotNull(configService);
            Assert.NotNull(monitoringService);
            Assert.NotNull(adapterFactory);
            Assert.NotNull(protocolRouter);
            Assert.NotNull(opcProvider);
            Assert.NotNull(dataService);
            Assert.NotNull(serviceManager);
        }

        [Fact]
        public void ServiceRegistrar_RegisterServices_ShouldRegisterServicesWithCorrectLifetime()
        {
            // Arrange & Act
            var container = ServiceRegistrar.RegisterServices();

            // Assert
            // 验证单例服务
            var configService1 = container.GetService<IConfigurationService>();
            var configService2 = container.GetService<IConfigurationService>();
            Assert.Same(configService1, configService2);

            var monitoringService1 = container.GetService<IMonitoringService>();
            var monitoringService2 = container.GetService<IMonitoringService>();
            Assert.Same(monitoringService1, monitoringService2);

            var adapterFactory1 = container.GetService<IProtocolAdapterFactory>();
            var adapterFactory2 = container.GetService<IProtocolAdapterFactory>();
            Assert.Same(adapterFactory1, adapterFactory2);

            var protocolRouter1 = container.GetService<IProtocolRouter>();
            var protocolRouter2 = container.GetService<IProtocolRouter>();
            Assert.Same(protocolRouter1, protocolRouter2);

            var opcProvider1 = container.GetService<IOpcDataProvider>();
            var opcProvider2 = container.GetService<IOpcDataProvider>();
            Assert.Same(opcProvider1, opcProvider2);

            var dataService1 = container.GetService<IDataService>();
            var dataService2 = container.GetService<IDataService>();
            Assert.Same(dataService1, dataService2);

            var serviceManager1 = container.GetService<IServiceManager>();
            var serviceManager2 = container.GetService<IServiceManager>();
            Assert.Same(serviceManager1, serviceManager2);
        }

        [Fact]
        public async Task ApplicationBootstrapper_Initialize_ShouldInitializeServices()
        {
            // Arrange & Act
            ApplicationBootstrapper.Initialize();

            // Assert
            var serviceProvider = ApplicationBootstrapper.ServiceProvider;
            Assert.NotNull(serviceProvider);

            // 验证所有服务都能正确解析
            var configService = serviceProvider.GetService<IConfigurationService>();
            var serviceManager = serviceProvider.GetService<IServiceManager>();

            Assert.NotNull(configService);
            Assert.NotNull(serviceManager);
        }

        [Fact]
        public async Task ApplicationBootstrapper_StartAsync_ShouldStartAllServices()
        {
            // Arrange
            ApplicationBootstrapper.Initialize();

            // Act
            var result = await ApplicationBootstrapper.StartAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ApplicationBootstrapper_StopAsync_ShouldStopAllServices()
        {
            // Arrange
            ApplicationBootstrapper.Initialize();
            await ApplicationBootstrapper.StartAsync();

            // Act
            var result = await ApplicationBootstrapper.StopAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ServiceContainer_WithComplexDependencies_ShouldResolveCorrectly()
        {
            // Arrange
            var container = new ServiceContainer();
            container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);
            container.Register<IProtocolAdapterFactory, MockProtocolAdapterFactory>(ServiceLifetime.Singleton);
            container.Register<IProtocolRouter, MockProtocolRouter>(ServiceLifetime.Singleton);
            container.Register<IOpcDataProvider, MockOpcDataProvider>(ServiceLifetime.Singleton);
            container.Register<IMonitoringService, MockMonitoringService>(ServiceLifetime.Singleton);
            container.Register<IDataService, MockOpcDataService>(ServiceLifetime.Singleton);
            container.Register<IServiceManager, MockServiceManager>(ServiceLifetime.Singleton);

            // Act
            var serviceManager = container.GetService<IServiceManager>();

            // Assert
            Assert.NotNull(serviceManager);
            Assert.IsType<MockServiceManager>(serviceManager);
        }

        [Fact]
        public async Task FullIntegrationTest_StartStopServices_ShouldWorkCorrectly()
        {
            // Arrange
            var container = new ServiceContainer();
            container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);
            container.Register<IProtocolAdapterFactory, MockProtocolAdapterFactory>(ServiceLifetime.Singleton);
            container.Register<IProtocolRouter, MockProtocolRouter>(ServiceLifetime.Singleton);
            container.Register<IOpcDataProvider, MockOpcDataProvider>(ServiceLifetime.Singleton);
            container.Register<IMonitoringService, MockMonitoringService>(ServiceLifetime.Singleton);
            container.Register<IDataService, MockOpcDataService>(ServiceLifetime.Singleton);
            container.Register<IServiceManager, MockServiceManager>(ServiceLifetime.Singleton);

            var serviceManager = container.GetService<IServiceManager>();

            // Act & Assert
            var startResult = await serviceManager.StartAllServicesAsync();
            Assert.True(startResult);

            var status = serviceManager.GetServiceStatus();
            Assert.NotNull(status);

            var stopResult = await serviceManager.StopAllServicesAsync();
            Assert.True(stopResult);
        }
    }

    #region Mock Implementations for Integration Tests

    /// <summary>
    /// 模拟OPC数据服务
    /// </summary>
    public class MockOpcDataService : IDataService
    {
        public bool IsRunning { get; set; } = false;

        public Task<bool> StartAsync()
        {
            IsRunning = true;
            return Task.FromResult(true);
        }

        public Task<bool> StopAsync()
        {
            IsRunning = false;
            return Task.FromResult(true);
        }

        public Task<ItemValueResult[]> ReadDataAsync()
        {
            return Task.FromResult(new ItemValueResult[0]);
        }

        public Task<bool> SendDataAsync(ItemValueResult[] data)
        {
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// 模拟服务管理器
    /// </summary>
    public class MockServiceManager : IServiceManager
    {
        private readonly IDataService dataService;
        private readonly IMonitoringService monitoringService;

        public MockServiceManager(IDataService dataService, IMonitoringService monitoringService)
        {
            this.dataService = dataService;
            this.monitoringService = monitoringService;
        }

        public async Task<bool> StartAllServicesAsync()
        {
            monitoringService.Start();
            var result = await dataService.StartAsync();
            return result;
        }

        public async Task<bool> StopAllServicesAsync()
        {
            await dataService.StopAsync();
            monitoringService.Stop();
            return true;
        }

        public ServiceStatus GetServiceStatus()
        {
            return new ServiceStatus
            {
                DataServiceRunning = dataService.IsRunning,
                MonitoringServiceRunning = monitoringService.IsRunning,
                HealthReport = monitoringService.GetHealthReport()
            };
        }
    }

    #endregion
}
