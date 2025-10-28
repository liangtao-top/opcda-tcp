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
    /// OPC数据服务单元测试
    /// </summary>
    public class OpcDataServiceTests : TestBase
    {
        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitialize()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();

            // Act
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);

            // Assert
            Assert.NotNull(service);
            Assert.False(service.IsRunning);
        }

        [Fact]
        public void Constructor_WithNullOpcProvider_ShouldThrowArgumentNullException()
        {
            // Arrange
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new OpcDataService(null, protocolRouter, configService, monitoringService));
        }

        [Fact]
        public void Constructor_WithNullProtocolRouter_ShouldThrowArgumentNullException()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new OpcDataService(opcProvider, null, configService, monitoringService));
        }

        [Fact]
        public void Constructor_WithNullConfigurationService_ShouldThrowArgumentNullException()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var monitoringService = CreateMockMonitoringService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new OpcDataService(opcProvider, protocolRouter, null, monitoringService));
        }

        [Fact]
        public void Constructor_WithNullMonitoringService_ShouldThrowArgumentNullException()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new OpcDataService(opcProvider, protocolRouter, configService, null));
        }

        [Fact]
        public async Task StartAsync_WithValidDependencies_ShouldReturnTrue()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);

            // Act
            var result = await service.StartAsync();

            // Assert
            Assert.True(result);
            Assert.True(service.IsRunning);
        }

        [Fact]
        public async Task StartAsync_WithFailedProtocolRouterInitialization_ShouldReturnFalse()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = new MockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);

            // 模拟协议路由器初始化失败
            protocolRouter.InitializeAsync = () => Task.FromResult(false);

            // Act
            var result = await service.StartAsync();

            // Assert
            Assert.False(result);
            Assert.False(service.IsRunning);
        }

        [Fact]
        public async Task StartAsync_WithFailedOpcConnection_ShouldReturnFalse()
        {
            // Arrange
            var opcProvider = new MockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);

            // 模拟OPC连接失败
            opcProvider.ConnectAsync = () => Task.FromResult(false);

            // Act
            var result = await service.StartAsync();

            // Assert
            Assert.False(result);
            Assert.False(service.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldReturnTrue()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);
            await service.StartAsync();

            // Act
            var result = await service.StopAsync();

            // Assert
            Assert.True(result);
            Assert.False(service.IsRunning);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldReturnTrue()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);

            // Act
            var result = await service.StopAsync();

            // Assert
            Assert.True(result);
            Assert.False(service.IsRunning);
        }

        [Fact]
        public async Task ReadDataAsync_WhenRunning_ShouldReturnData()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);
            await service.StartAsync();
            var expectedData = CreateTestOpcData();
            ((MockOpcDataProvider)opcProvider).SetTestData(expectedData);

            // Act
            var result = await service.ReadDataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedData.Length, result.Length);
        }

        [Fact]
        public async Task ReadDataAsync_WhenNotRunning_ShouldReturnEmptyArray()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);

            // Act
            var result = await service.ReadDataAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task SendDataAsync_WhenRunning_ShouldReturnTrue()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);
            await service.StartAsync();
            var testData = CreateTestOpcData();

            // Act
            var result = await service.SendDataAsync(testData);

            // Assert
            Assert.True(result);
            Assert.Single(((MockProtocolRouter)protocolRouter).SentData);
        }

        [Fact]
        public async Task SendDataAsync_WhenNotRunning_ShouldReturnFalse()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);
            var testData = CreateTestOpcData();

            // Act
            var result = await service.SendDataAsync(testData);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SendDataAsync_WithNullData_ShouldReturnTrue()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);
            await service.StartAsync();

            // Act
            var result = await service.SendDataAsync(null);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SendDataAsync_WithEmptyData_ShouldReturnTrue()
        {
            // Arrange
            var opcProvider = CreateMockOpcDataProvider();
            var protocolRouter = CreateMockProtocolRouter();
            var configService = CreateMockConfigurationService();
            var monitoringService = CreateMockMonitoringService();
            var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);
            await service.StartAsync();

            // Act
            var result = await service.SendDataAsync(new ItemValueResult[0]);

            // Assert
            Assert.True(result);
        }
    }
}
