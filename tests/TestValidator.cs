using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Services;
using OpcDAToMSA.Protocols;
using OpcDAToMSA.Core;
using OpcDAToMSA.DependencyInjection;
using OpcDAToMSA.Monitoring;
using Opc.Da;

namespace OpcDAToMSA.Tests.Unit
{
    /// <summary>
    /// 简化的测试验证器 - 不依赖外部测试框架
    /// </summary>
    public class TestValidator
    {
        private int totalTests = 0;
        private int passedTests = 0;
        private int failedTests = 0;
        private List<string> testResults = new List<string>();

        public void RunAllTests()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("OpcDAToMSA 依赖注入重构测试验证");
            Console.WriteLine("========================================");
            Console.WriteLine();

            // 运行所有测试
            RunConfigurationServiceTests();
            RunProtocolAdapterTests();
            RunDataServiceTests();
            RunServiceManagerTests();
            RunDependencyInjectionTests();
            RunIntegrationTests();

            // 输出测试结果
            PrintTestResults();
        }

        private void RunTest(string testName, Func<bool> testAction)
        {
            totalTests++;
            try
            {
                bool result = testAction();
                if (result)
                {
                    passedTests++;
                    testResults.Add($"✅ {testName}");
                }
                else
                {
                    failedTests++;
                    testResults.Add($"❌ {testName}");
                }
            }
            catch (Exception ex)
            {
                failedTests++;
                testResults.Add($"❌ {testName} - 异常: {ex.Message}");
            }
        }

        private void RunConfigurationServiceTests()
        {
            Console.WriteLine("🧪 运行配置服务测试...");
            
            RunTest("配置服务 - 构造函数测试", () =>
            {
                var service = new MockConfigurationService();
                return service != null;
            });

            RunTest("配置服务 - 获取配置测试", () =>
            {
                var service = new MockConfigurationService();
                var config = service.GetConfiguration();
                return config != null;
            });

            RunTest("配置服务 - 重新加载配置测试", () =>
            {
                var service = new MockConfigurationService();
                service.ReloadConfiguration();
                return true; // 如果没有异常就认为成功
            });

            Console.WriteLine();
        }

        private void RunProtocolAdapterTests()
        {
            Console.WriteLine("🧪 运行协议适配器测试...");
            
            RunTest("MQTT适配器 - 构造函数测试", () =>
            {
                var configService = new MockConfigurationService();
                var adapter = new MqttAdapter(configService);
                return adapter != null && adapter.ProtocolName == "MQTT";
            });

            RunTest("MQTT适配器 - 空参数异常测试", () =>
            {
                try
                {
                    var adapter = new MqttAdapter(null);
                    return false; // 应该抛出异常
                }
                catch (ArgumentNullException)
                {
                    return true; // 期望的异常
                }
            });

            RunTest("Modbus TCP适配器 - 构造函数测试", () =>
            {
                var configService = new MockConfigurationService();
                var adapter = new ModbusTcpAdapter(configService);
                return adapter != null && adapter.ProtocolName == "Modbus TCP";
            });

            RunTest("MSA适配器 - 构造函数测试", () =>
            {
                var configService = new MockConfigurationService();
                var adapter = new MsaAdapter(configService);
                return adapter != null && adapter.ProtocolName == "MSA";
            });

            Console.WriteLine();
        }

        private void RunDataServiceTests()
        {
            Console.WriteLine("🧪 运行数据服务测试...");
            
            RunTest("OPC数据服务 - 构造函数测试", () =>
            {
                var opcProvider = new MockOpcDataProvider();
                var protocolRouter = new MockProtocolRouter();
                var configService = new MockConfigurationService();
                var monitoringService = new MockMonitoringService();
                var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);
                return service != null;
            });

            RunTest("OPC数据服务 - 空参数异常测试", () =>
            {
                try
                {
                    var service = new OpcDataService(null, null, null, null);
                    return false; // 应该抛出异常
                }
                catch (ArgumentNullException)
                {
                    return true; // 期望的异常
                }
            });

            RunTest("OPC数据服务 - 启动测试", async () =>
            {
                var opcProvider = new MockOpcDataProvider();
                var protocolRouter = new MockProtocolRouter();
                var configService = new MockConfigurationService();
                var monitoringService = new MockMonitoringService();
                var service = new OpcDataService(opcProvider, protocolRouter, configService, monitoringService);
                return await service.StartAsync();
            });

            Console.WriteLine();
        }

        private void RunServiceManagerTests()
        {
            Console.WriteLine("🧪 运行服务管理器测试...");
            
            RunTest("服务管理器 - 构造函数测试", () =>
            {
                var dataService = new MockDataService();
                var monitoringService = new MockMonitoringService();
                var serviceManager = new ServiceManager(dataService, monitoringService);
                return serviceManager != null;
            });

            RunTest("服务管理器 - 空参数异常测试", () =>
            {
                try
                {
                    var serviceManager = new ServiceManager(null, null);
                    return false; // 应该抛出异常
                }
                catch (ArgumentNullException)
                {
                    return true; // 期望的异常
                }
            });

            RunTest("服务管理器 - 启动服务测试", async () =>
            {
                var dataService = new MockDataService();
                var monitoringService = new MockMonitoringService();
                var serviceManager = new ServiceManager(dataService, monitoringService);
                return await serviceManager.StartAllServicesAsync();
            });

            Console.WriteLine();
        }

        private void RunDependencyInjectionTests()
        {
            Console.WriteLine("🧪 运行依赖注入测试...");
            
            RunTest("服务容器 - 注册服务测试", () =>
            {
                var container = new ServiceContainer();
                container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);
                return true; // 如果没有异常就认为成功
            });

            RunTest("服务容器 - 解析服务测试", () =>
            {
                var container = new ServiceContainer();
                container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);
                var service = container.GetService<IConfigurationService>();
                return service != null;
            });

            RunTest("服务容器 - 单例生命周期测试", () =>
            {
                var container = new ServiceContainer();
                container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Singleton);
                var service1 = container.GetService<IConfigurationService>();
                var service2 = container.GetService<IConfigurationService>();
                return ReferenceEquals(service1, service2);
            });

            RunTest("服务容器 - 瞬时生命周期测试", () =>
            {
                var container = new ServiceContainer();
                container.Register<IConfigurationService, MockConfigurationService>(ServiceLifetime.Transient);
                var service1 = container.GetService<IConfigurationService>();
                var service2 = container.GetService<IConfigurationService>();
                return !ReferenceEquals(service1, service2);
            });

            Console.WriteLine();
        }

        private void RunIntegrationTests()
        {
            Console.WriteLine("🧪 运行集成测试...");
            
            RunTest("服务注册器 - 注册所有服务测试", () =>
            {
                var container = ServiceRegistrar.RegisterServices();
                return container != null;
            });

            RunTest("应用程序启动器 - 初始化测试", () =>
            {
                try
                {
                    ApplicationBootstrapper.Initialize();
                    return ApplicationBootstrapper.ServiceProvider != null;
                }
                catch
                {
                    return false;
                }
            });

            RunTest("完整集成 - 服务解析测试", () =>
            {
                try
                {
                    ApplicationBootstrapper.Initialize();
                    var configService = ApplicationBootstrapper.ServiceProvider.GetService<IConfigurationService>();
                    var serviceManager = ApplicationBootstrapper.ServiceProvider.GetService<IServiceManager>();
                    return configService != null && serviceManager != null;
                }
                catch
                {
                    return false;
                }
            });

            Console.WriteLine();
        }

        private void PrintTestResults()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("测试结果汇总");
            Console.WriteLine("========================================");
            Console.WriteLine($"总测试数: {totalTests}");
            Console.WriteLine($"通过: {passedTests}");
            Console.WriteLine($"失败: {failedTests}");
            Console.WriteLine($"成功率: {(totalTests > 0 ? (passedTests * 100.0 / totalTests).ToString("F2") : "0")}%");
            Console.WriteLine();

            Console.WriteLine("详细结果:");
            foreach (var result in testResults)
            {
                Console.WriteLine(result);
            }

            Console.WriteLine();
            Console.WriteLine("========================================");
            if (failedTests == 0)
            {
                Console.WriteLine("🎉 所有测试通过！依赖注入重构验证成功！");
            }
            else
            {
                Console.WriteLine($"⚠️  有 {failedTests} 个测试失败，需要检查。");
            }
            Console.WriteLine("========================================");
        }
    }

    /// <summary>
    /// 程序入口点
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var validator = new TestValidator();
            validator.RunAllTests();
            
            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}
