using System;
using System.Threading.Tasks;
using OpcDAToMSA.Services;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Protocols;
using OpcDAToMSA.Core;
using OpcDAToMSA.Monitoring;
using OpcDAToMSA.Tests.Unit;
using Opc.Da;
using Xunit;

namespace OpcDAToMSA.Tests.Unit.Services
{
    /// <summary>
    /// 服务管理器单元测试
    /// </summary>
    public class ServiceManagerTests : TestBase
    {
        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitialize()
        {
            // Arrange
            var dataService = new MockDataService();
            var monitoringService = CreateMockMonitoringService();

            // Act
            var serviceManager = new ServiceManager(dataService, monitoringService);

            // Assert
            Assert.NotNull(serviceManager);
        }

        [Fact]
        public void Constructor_WithNullDataService_ShouldThrowArgumentNullException()
        {
            // Arrange
            var monitoringService = CreateMockMonitoringService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ServiceManager(null, monitoringService));
        }

        [Fact]
        public void Constructor_WithNullMonitoringService_ShouldThrowArgumentNullException()
        {
            // Arrange
            var dataService = new MockDataService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ServiceManager(dataService, null));
        }

        [Fact]
        public async Task StartAllServicesAsync_WithValidServices_ShouldReturnTrue()
        {
            // Arrange
            var dataService = new MockDataService();
            var monitoringService = CreateMockMonitoringService();
            var serviceManager = new ServiceManager(dataService, monitoringService);

            // Act
            var result = await serviceManager.StartAllServicesAsync();

            // Assert
            Assert.True(result);
            Assert.True(dataService.IsStarted);
            Assert.True(monitoringService.IsRunning);
        }

        [Fact]
        public async Task StartAllServicesAsync_WithFailedDataService_ShouldReturnFalse()
        {
            // Arrange
            var dataService = new MockDataService();
            var monitoringService = CreateMockMonitoringService();
            var serviceManager = new ServiceManager(dataService, monitoringService);

            // 模拟数据服务启动失败
            dataService.StartAsync = () => Task.FromResult(false);

            // Act
            var result = await serviceManager.StartAllServicesAsync();

            // Assert
            Assert.False(result);
            Assert.False(dataService.IsStarted);
            Assert.True(monitoringService.IsRunning); // 监控服务应该仍然启动
        }

        [Fact]
        public async Task StopAllServicesAsync_WhenRunning_ShouldReturnTrue()
        {
            // Arrange
            var dataService = new MockDataService();
            var monitoringService = CreateMockMonitoringService();
            var serviceManager = new ServiceManager(dataService, monitoringService);
            await serviceManager.StartAllServicesAsync();

            // Act
            var result = await serviceManager.StopAllServicesAsync();

            // Assert
            Assert.True(result);
            Assert.False(dataService.IsStarted);
            Assert.False(monitoringService.IsRunning);
        }

        [Fact]
        public async Task StopAllServicesAsync_WhenNotRunning_ShouldReturnTrue()
        {
            // Arrange
            var dataService = new MockDataService();
            var monitoringService = CreateMockMonitoringService();
            var serviceManager = new ServiceManager(dataService, monitoringService);

            // Act
            var result = await serviceManager.StopAllServicesAsync();

            // Assert
            Assert.True(result);
            Assert.False(dataService.IsStarted);
            Assert.False(monitoringService.IsRunning);
        }

        [Fact]
        public void GetServiceStatus_ShouldReturnCorrectStatus()
        {
            // Arrange
            var dataService = new MockDataService();
            var monitoringService = CreateMockMonitoringService();
            var serviceManager = new ServiceManager(dataService, monitoringService);

            // Act
            var status = serviceManager.GetServiceStatus();

            // Assert
            Assert.NotNull(status);
            Assert.False(status.DataServiceRunning);
            Assert.False(status.MonitoringServiceRunning);
            Assert.NotNull(status.HealthReport);
        }

        [Fact]
        public async Task GetServiceStatus_AfterStart_ShouldReturnRunningStatus()
        {
            // Arrange
            var dataService = new MockDataService();
            var monitoringService = CreateMockMonitoringService();
            var serviceManager = new ServiceManager(dataService, monitoringService);
            await serviceManager.StartAllServicesAsync();

            // Act
            var status = serviceManager.GetServiceStatus();

            // Assert
            Assert.NotNull(status);
            Assert.True(status.DataServiceRunning);
            Assert.True(status.MonitoringServiceRunning);
            Assert.NotNull(status.HealthReport);
        }

        [Fact]
        public async Task StartAllServicesAsync_ShouldStartServicesInCorrectOrder()
        {
            // Arrange
            var dataService = new MockDataService();
            var monitoringService = CreateMockMonitoringService();
            var serviceManager = new ServiceManager(dataService, monitoringService);

            // Act
            await serviceManager.StartAllServicesAsync();

            // Assert
            // 验证监控服务先启动
            Assert.True(monitoringService.IsRunning);
            // 验证数据服务后启动
            Assert.True(dataService.IsStarted);
        }

        [Fact]
        public async Task StopAllServicesAsync_ShouldStopServicesInCorrectOrder()
        {
            // Arrange
            var dataService = new MockDataService();
            var monitoringService = CreateMockMonitoringService();
            var serviceManager = new ServiceManager(dataService, monitoringService);
            await serviceManager.StartAllServicesAsync();

            // Act
            await serviceManager.StopAllServicesAsync();

            // Assert
            // 验证数据服务先停止
            Assert.False(dataService.IsStarted);
            // 验证监控服务后停止
            Assert.False(monitoringService.IsRunning);
        }
    }

    /// <summary>
    /// 模拟数据服务
    /// </summary>
    public class MockDataService : IDataService
    {
        public bool IsStarted { get; set; } = false;
        public bool IsRunning => IsStarted;

        public Func<Task<bool>> StartAsync { get; set; } = () => Task.FromResult(true);
        public Func<Task<bool>> StopAsync { get; set; } = () => Task.FromResult(true);
        public Func<Task<ItemValueResult[]>> ReadDataAsync { get; set; } = () => Task.FromResult(new ItemValueResult[0]);
        public Func<ItemValueResult[], Task<bool>> SendDataAsync { get; set; } = (data) => Task.FromResult(true);

        public async Task<bool> StartAsync()
        {
            var result = await StartAsync();
            if (result)
            {
                IsStarted = true;
            }
            return result;
        }

        public async Task<bool> StopAsync()
        {
            var result = await StopAsync();
            if (result)
            {
                IsStarted = false;
            }
            return result;
        }

        public Task<ItemValueResult[]> ReadDataAsync()
        {
            return ReadDataAsync();
        }

        public Task<bool> SendDataAsync(ItemValueResult[] data)
        {
            return SendDataAsync(data);
        }
    }
}
