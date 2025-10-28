using System;
using System.Threading.Tasks;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Tests.Unit;
using Xunit;

namespace OpcDAToMSA.Tests.Unit.Configuration
{
    /// <summary>
    /// 配置服务单元测试
    /// </summary>
    public class ConfigurationServiceTests : TestBase
    {
        [Fact]
        public void GetConfiguration_ShouldReturnValidConfiguration()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var expectedConfig = CreateTestConfiguration();
            ((MockConfigurationService)configService).SetConfiguration(expectedConfig);

            // Act
            var actualConfig = configService.GetConfiguration();

            // Assert
            Assert.NotNull(actualConfig);
            Assert.Equal(expectedConfig.AutoStart, actualConfig.AutoStart);
            Assert.NotNull(actualConfig.OpcDa);
            Assert.NotNull(actualConfig.Registers);
            Assert.NotNull(actualConfig.Protocols);
        }

        [Fact]
        public void ReloadConfiguration_ShouldTriggerConfigurationChangedEvent()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var eventTriggered = false;
            configService.ConfigurationChanged += (sender, e) => eventTriggered = true;

            // Act
            configService.ReloadConfiguration();

            // Assert
            Assert.True(eventTriggered);
        }

        [Fact]
        public void ConfigurationChanged_ShouldProvideOldAndNewConfig()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var oldConfig = CreateTestConfiguration();
            var newConfig = CreateTestConfiguration();
            newConfig.AutoStart = !oldConfig.AutoStart;

            CfgJson receivedOldConfig = null;
            CfgJson receivedNewConfig = null;

            configService.ConfigurationChanged += (sender, e) =>
            {
                receivedOldConfig = e.OldConfig;
                receivedNewConfig = e.NewConfig;
            };

            // Act
            ((MockConfigurationService)configService).SetConfiguration(oldConfig);
            ((MockConfigurationService)configService).SetConfiguration(newConfig);

            // Assert
            Assert.NotNull(receivedOldConfig);
            Assert.NotNull(receivedNewConfig);
            Assert.Equal(oldConfig.AutoStart, receivedOldConfig.AutoStart);
            Assert.Equal(newConfig.AutoStart, receivedNewConfig.AutoStart);
        }

        [Fact]
        public void GetConfiguration_WithNullConfig_ShouldReturnDefaultConfig()
        {
            // Arrange
            var configService = CreateMockConfigurationService();

            // Act
            var config = configService.GetConfiguration();

            // Assert
            Assert.NotNull(config);
            Assert.NotNull(config.OpcDa);
            Assert.NotNull(config.Registers);
            Assert.NotNull(config.Protocols);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetConfiguration_AutoStart_ShouldReturnCorrectValue(bool autoStart)
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var testConfig = CreateTestConfiguration();
            testConfig.AutoStart = autoStart;
            ((MockConfigurationService)configService).SetConfiguration(testConfig);

            // Act
            var config = configService.GetConfiguration();

            // Assert
            Assert.Equal(autoStart, config.AutoStart);
        }

        [Fact]
        public void GetConfiguration_Protocols_ShouldContainAllProtocols()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var testConfig = CreateTestConfiguration();
            ((MockConfigurationService)configService).SetConfiguration(testConfig);

            // Act
            var config = configService.GetConfiguration();

            // Assert
            Assert.True(config.Protocols.ContainsKey("msa"));
            Assert.True(config.Protocols.ContainsKey("mqtt"));
            Assert.True(config.Protocols.ContainsKey("modbusTcp"));
        }

        [Fact]
        public void GetConfiguration_Registers_ShouldContainTestRegisters()
        {
            // Arrange
            var configService = CreateMockConfigurationService();
            var testConfig = CreateTestConfiguration();
            ((MockConfigurationService)configService).SetConfiguration(testConfig);

            // Act
            var config = configService.GetConfiguration();

            // Assert
            Assert.True(config.Registers.ContainsKey("TestTag1"));
            Assert.True(config.Registers.ContainsKey("TestTag2"));
            Assert.True(config.Registers.ContainsKey("TestTag3"));
            Assert.Equal("1001", config.Registers["TestTag1"]);
            Assert.Equal("1002", config.Registers["TestTag2"]);
            Assert.Equal("1003", config.Registers["TestTag3"]);
        }
    }
}
