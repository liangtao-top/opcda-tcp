using System;
using System.Threading.Tasks;
using OpcDAToMSA.Protocols;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Tests.Unit;
using Opc.Da;
using Xunit;

namespace OpcDAToMSA.Tests.Unit.Protocols
{
    /// <summary>
    /// MQTT适配器单元测试
    /// </summary>
    public class MqttAdapterTests : TestBase
    {
        [Fact]
        public void Constructor_WithValidConfigurationService_ShouldInitialize()
        {
            // Arrange
            var configService = CreateMockConfigurationService();

            // Act
            var adapter = new MqttAdapter(configService);

            // Assert
            Assert.NotNull(adapter);
            Assert.Equal("MQTT", adapter.ProtocolName);
            Assert.True(adapter.IsEnabled);
            Assert.False(adapter.IsConnected);
        }

        [Fact]
        public void Constructor_WithNullConfigurationService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MqttAdapter(null));
        }

        [Fact]
        public async Task InitializeAsync_WithEnabledProtocol_ShouldReturnTrue()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var testConfig = CreateTestConfiguration();
            ((MockConfigurationService)configService).SetConfiguration(testConfig);
            var adapter = new MqttAdapter(configService);

            // Act
            var result = await adapter.InitializeAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task InitializeAsync_WithDisabledProtocol_ShouldReturnTrue()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var testConfig = CreateTestConfiguration();
            testConfig.Protocols["mqtt"].Enabled = false;
            ((MockConfigurationService)configService).SetConfiguration(testConfig);
            var adapter = new MqttAdapter(configService);

            // Act
            var result = await adapter.InitializeAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SendDataAsync_WithValidData_ShouldReturnTrue()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var testConfig = CreateTestConfiguration();
            ((MockConfigurationService)configService).SetConfiguration(testConfig);
            var adapter = new MqttAdapter(configService);
            await adapter.InitializeAsync();
            var testData = CreateTestOpcData();

            // Act
            var result = await adapter.SendDataAsync(testData);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SendDataAsync_WithNullData_ShouldReturnFalse()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var adapter = new MqttAdapter(configService);

            // Act
            var result = await adapter.SendDataAsync(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DisconnectAsync_ShouldReturnTrue()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var adapter = new MqttAdapter(configService);

            // Act
            var result = await adapter.DisconnectAsync();

            // Assert
            Assert.True(result);
        }
    }

    /// <summary>
    /// Modbus TCP适配器单元测试
    /// </summary>
    public class ModbusTcpAdapterTests : TestBase
    {
        [Fact]
        public void Constructor_WithValidConfigurationService_ShouldInitialize()
        {
            // Arrange
            var configService = CreateMockConfigurationService();

            // Act
            var adapter = new ModbusTcpAdapter(configService);

            // Assert
            Assert.NotNull(adapter);
            Assert.Equal("Modbus TCP", adapter.ProtocolName);
            Assert.True(adapter.IsEnabled);
            Assert.False(adapter.IsConnected);
        }

        [Fact]
        public void Constructor_WithNullConfigurationService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ModbusTcpAdapter(null));
        }

        [Fact]
        public async Task InitializeAsync_WithEnabledProtocol_ShouldReturnTrue()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var testConfig = CreateTestConfiguration();
            ((MockConfigurationService)configService).SetConfiguration(testConfig);
            var adapter = new ModbusTcpAdapter(configService);

            // Act
            var result = await adapter.InitializeAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SendDataAsync_WithValidData_ShouldReturnTrue()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var testConfig = CreateTestConfiguration();
            ((MockConfigurationService)configService).SetConfiguration(testConfig);
            var adapter = new ModbusTcpAdapter(configService);
            await adapter.InitializeAsync();
            var testData = CreateTestOpcData();

            // Act
            var result = await adapter.SendDataAsync(testData);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DisconnectAsync_ShouldReturnTrue()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var adapter = new ModbusTcpAdapter(configService);

            // Act
            var result = await adapter.DisconnectAsync();

            // Assert
            Assert.True(result);
        }
    }

    /// <summary>
    /// MSA适配器单元测试
    /// </summary>
    public class MsaAdapterTests : TestBase
    {
        [Fact]
        public void Constructor_WithValidConfigurationService_ShouldInitialize()
        {
            // Arrange
            var configService = CreateMockConfigurationService();

            // Act
            var adapter = new MsaAdapter(configService);

            // Assert
            Assert.NotNull(adapter);
            Assert.Equal("MSA", adapter.ProtocolName);
            Assert.True(adapter.IsEnabled);
            Assert.False(adapter.IsConnected);
        }

        [Fact]
        public void Constructor_WithNullConfigurationService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new MsaAdapter(null));
        }

        [Fact]
        public async Task InitializeAsync_WithEnabledProtocol_ShouldReturnTrue()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var testConfig = CreateTestConfiguration();
            ((MockConfigurationService)configService).SetConfiguration(testConfig);
            var adapter = new MsaAdapter(configService);

            // Act
            var result = await adapter.InitializeAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task SendDataAsync_WithValidData_ShouldReturnTrue()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var testConfig = CreateTestConfiguration();
            ((MockConfigurationService)configService).SetConfiguration(testConfig);
            var adapter = new MsaAdapter(configService);
            await adapter.InitializeAsync();
            var testData = CreateTestOpcData();

            // Act
            var result = await adapter.SendDataAsync(testData);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DisconnectAsync_ShouldReturnTrue()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var adapter = new MsaAdapter(configService);

            // Act
            var result = await adapter.DisconnectAsync();

            // Assert
            Assert.True(result);
        }
    }
}
