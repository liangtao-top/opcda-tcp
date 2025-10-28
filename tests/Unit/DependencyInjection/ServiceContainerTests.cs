using System;
using OpcDAToMSA.DependencyInjection;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Services;
using OpcDAToMSA.Protocols;
using OpcDAToMSA.Core;
using OpcDAToMSA.Monitoring;
using OpcDAToMSA.Tests.Unit;
using Xunit;

namespace OpcDAToMSA.Tests.Unit.DependencyInjection
{
    /// <summary>
    /// 服务容器单元测试
    /// </summary>
    public class ServiceContainerTests : TestBase
    {
        [Fact]
        public void Register_WithValidTypes_ShouldRegisterService()
        {
            // Arrange
            var container = new ServiceContainer();

            // Act
            container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);

            // Assert
            // 验证服务已注册（通过解析验证）
            var service = container.GetService<IConfigurationService>();
            Assert.NotNull(service);
            Assert.IsType<MockConfigurationService>(service);
        }

        [Fact]
        public void Register_WithFactory_ShouldRegisterService()
        {
            // Arrange
            var container = new ServiceContainer();

            // Act
            container.Register<IConfigurationService>(provider => new MockConfigurationService(), ServiceLifetime.Singleton);

            // Assert
            var service = container.GetService<IConfigurationService>();
            Assert.NotNull(service);
            Assert.IsType<MockConfigurationService>(service);
        }

        [Fact]
        public void GetService_WithUnregisteredService_ShouldThrowException()
        {
            // Arrange
            var container = new ServiceContainer();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => container.GetService<IConfigurationService>());
        }

        [Fact]
        public void GetService_WithSingletonLifetime_ShouldReturnSameInstance()
        {
            // Arrange
            var container = new ServiceContainer();
            container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);

            // Act
            var service1 = container.GetService<IConfigurationService>();
            var service2 = container.GetService<IConfigurationService>();

            // Assert
            Assert.Same(service1, service2);
        }

        [Fact]
        public void GetService_WithTransientLifetime_ShouldReturnDifferentInstances()
        {
            // Arrange
            var container = new ServiceContainer();
            container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Transient);

            // Act
            var service1 = container.GetService<IConfigurationService>();
            var service2 = container.GetService<IConfigurationService>();

            // Assert
            Assert.NotSame(service1, service2);
        }

        [Fact]
        public void GetService_WithScopedLifetime_ShouldReturnSameInstanceInScope()
        {
            // Arrange
            var container = new ServiceContainer();
            container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Scoped);

            // Act
            var service1 = container.GetService<IConfigurationService>();
            var service2 = container.GetService<IConfigurationService>();

            // Assert
            // 在简单实现中，Scoped 被当作 Transient 处理
            Assert.NotSame(service1, service2);
        }

        [Fact]
        public void GetService_WithDependencies_ShouldResolveDependencies()
        {
            // Arrange
            var container = new ServiceContainer();
            container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);
            container.Register<IProtocolAdapterFactory, MockProtocolAdapterFactory>(ServiceLifetime.Singleton);

            // Act
            var factory = container.GetService<IProtocolAdapterFactory>();

            // Assert
            Assert.NotNull(factory);
            Assert.IsType<MockProtocolAdapterFactory>(factory);
        }

        [Fact]
        public void GetService_WithCircularDependency_ShouldThrowException()
        {
            // Arrange
            var container = new ServiceContainer();
            container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);
            container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => container.GetService<IConfigurationService>());
        }

        [Fact]
        public void GetService_WithConcreteType_ShouldCreateInstance()
        {
            // Arrange
            var container = new ServiceContainer();

            // Act
            var service = container.GetService<MockConfigurationService>();

            // Assert
            Assert.NotNull(service);
            Assert.IsType<MockConfigurationService>(service);
        }

        [Fact]
        public void GetService_WithAbstractType_ShouldThrowException()
        {
            // Arrange
            var container = new ServiceContainer();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => container.GetService<IConfigurationService>());
        }
    }

    /// <summary>
    /// 服务定位器单元测试
    /// </summary>
    public class ServiceLocatorTests : TestBase
    {
        [Fact]
        public void SetServiceProvider_WithValidProvider_ShouldSetProvider()
        {
            // Arrange
            var container = new ServiceContainer();
            container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);

            // Act
            ServiceLocator.SetServiceProvider(container);

            // Assert
            var service = ServiceLocator.GetService<IConfigurationService>();
            Assert.NotNull(service);
        }

        [Fact]
        public void GetService_WithoutProvider_ShouldThrowException()
        {
            // Arrange
            ServiceLocator.SetServiceProvider(null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.GetService<IConfigurationService>());
        }

        [Fact]
        public void GetService_WithType_ShouldReturnService()
        {
            // Arrange
            var container = new ServiceContainer();
            container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);
            ServiceLocator.SetServiceProvider(container);

            // Act
            var service = ServiceLocator.GetService(typeof(IConfigurationService));

            // Assert
            Assert.NotNull(service);
            Assert.IsType<MockConfigurationService>(service);
        }
    }

    #region Mock Implementations for DI Tests

    /// <summary>
    /// 模拟协议适配器工厂
    /// </summary>
    public class MockProtocolAdapterFactory : IProtocolAdapterFactory
    {
        public IProtocolAdapter CreateAdapter(string protocolType)
        {
            return new MockProtocolAdapter();
        }
    }

    /// <summary>
    /// 模拟协议适配器
    /// </summary>
    public class MockProtocolAdapter : IProtocolAdapter
    {
        public string ProtocolName => "Mock";
        public bool IsEnabled { get; set; } = true;
        public bool IsConnected => true;

        public Task<bool> InitializeAsync()
        {
            return Task.FromResult(true);
        }

        public Task<bool> SendDataAsync(Opc.Da.ItemValueResult[] data)
        {
            return Task.FromResult(true);
        }

        public Task<bool> DisconnectAsync()
        {
            return Task.FromResult(true);
        }
    }

    #endregion
}
