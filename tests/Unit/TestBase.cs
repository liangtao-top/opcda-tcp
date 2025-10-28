using System;
using System.Collections.Generic;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Services;
using OpcDAToMSA.Protocols;
using OpcDAToMSA.Core;
using Opc.Da;
using OpcDAToMSA.Monitoring;

namespace OpcDAToMSA.Tests.Unit
{
    /// <summary>
    /// 测试基类，提供通用的模拟对象和测试工具
    /// </summary>
    public abstract class TestBase
    {
        /// <summary>
        /// 创建模拟的配置服务
        /// </summary>
        protected IConfigurationService CreateMockConfigurationService()
        {
            return new MockConfigurationService();
        }

        /// <summary>
        /// 创建模拟的OPC数据提供者
        /// </summary>
        protected IOpcDataProvider CreateMockOpcDataProvider()
        {
            return new MockOpcDataProvider();
        }

        /// <summary>
        /// 创建模拟的协议路由器
        /// </summary>
        protected IProtocolRouter CreateMockProtocolRouter()
        {
            return new MockProtocolRouter();
        }

        /// <summary>
        /// 创建模拟的监控服务
        /// </summary>
        protected MonitoringService CreateMockMonitoringService()
        {
            return new MockMonitoringService();
        }

        /// <summary>
        /// 创建测试用的配置数据
        /// </summary>
        protected CfgJson CreateTestConfiguration()
        {
            return new CfgJson
            {
                AutoStart = false,
                Logger = new LoggerConfig
                {
                    Level = "Information",
                    OutputToConsole = true,
                    OutputToFile = true,
                    FilePath = "test.log"
                },
                OpcDa = new OpcDaConfig
                {
                    ServerName = "TestServer",
                    Host = "localhost",
                    Domain = "",
                    UserName = "",
                    Password = "",
                    UpdateRate = 1000,
                    Enabled = true
                },
                Registers = new Dictionary<string, string>
                {
                    ["TestTag1"] = "1001",
                    ["TestTag2"] = "1002",
                    ["TestTag3"] = "1003"
                },
                Protocols = new Dictionary<string, ProtocolConfig>
                {
                    ["msa"] = new ProtocolConfig
                    {
                        Enabled = true,
                        Settings = new Dictionary<string, object>
                        {
                            ["ip"] = "127.0.0.1",
                            ["port"] = 31100,
                            ["mn"] = 100000000,
                            ["heartbeat"] = 5000
                        }
                    },
                    ["mqtt"] = new ProtocolConfig
                    {
                        Enabled = true,
                        Settings = new Dictionary<string, object>
                        {
                            ["broker"] = "127.0.0.1",
                            ["port"] = 1883,
                            ["clientId"] = "test-client",
                            ["topic"] = "test/topic"
                        }
                    },
                    ["modbusTcp"] = new ProtocolConfig
                    {
                        Enabled = true,
                        Settings = new Dictionary<string, object>
                        {
                            ["ip"] = "127.0.0.1",
                            ["port"] = 502,
                            ["stationId"] = 1
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 创建测试用的OPC数据
        /// </summary>
        protected ItemValueResult[] CreateTestOpcData()
        {
            return new ItemValueResult[]
            {
                new ItemValueResult
                {
                    ItemName = "TestTag1",
                    Value = 100.5,
                    Quality = Opc.Da.Quality.Good,
                    Timestamp = DateTime.Now
                },
                new ItemValueResult
                {
                    ItemName = "TestTag2",
                    Value = "TestValue",
                    Quality = Opc.Da.Quality.Good,
                    Timestamp = DateTime.Now
                },
                new ItemValueResult
                {
                    ItemName = "TestTag3",
                    Value = true,
                    Quality = Opc.Da.Quality.Good,
                    Timestamp = DateTime.Now
                }
            };
        }
    }

    #region Mock Implementations

    /// <summary>
    /// 模拟配置服务
    /// </summary>
    public class MockConfigurationService : IConfigurationService
    {
        private CfgJson _config;

        public MockConfigurationService()
        {
            _config = new CfgJson();
        }

        public CfgJson GetConfiguration()
        {
            return _config;
        }

        public void ReloadConfiguration()
        {
            // 模拟重新加载配置
        }

        public event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

        public void SetConfiguration(CfgJson config)
        {
            var oldConfig = _config;
            _config = config;
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(oldConfig, _config));
        }
    }

    /// <summary>
    /// 模拟OPC数据提供者
    /// </summary>
    public class MockOpcDataProvider : IOpcDataProvider
    {
        public bool IsConnected { get; set; } = true;
        private ItemValueResult[] _testData;

        public MockOpcDataProvider()
        {
            _testData = new ItemValueResult[]
            {
                new ItemValueResult
                {
                    ItemName = "TestTag1",
                    Value = 100.5,
                    Quality = Opc.Da.Quality.Good,
                    Timestamp = DateTime.Now
                }
            };
        }

        public Task<bool> ConnectAsync()
        {
            IsConnected = true;
            return Task.FromResult(true);
        }

        public Task<ItemValueResult[]> ReadDataAsync()
        {
            return Task.FromResult(_testData);
        }

        public Task<bool> StartReadingAsync(Func<ItemValueResult[], Task> onDataReceived)
        {
            // 模拟异步数据读取
            Task.Run(async () =>
            {
                while (IsConnected)
                {
                    await Task.Delay(1000);
                    await onDataReceived(_testData);
                }
            });
            return Task.FromResult(true);
        }

        public Task<bool> StopReadingAsync()
        {
            IsConnected = false;
            return Task.FromResult(true);
        }

        public void SetTestData(ItemValueResult[] data)
        {
            _testData = data;
        }
    }

    /// <summary>
    /// 模拟协议路由器
    /// </summary>
    public class MockProtocolRouter : IProtocolRouter
    {
        public bool IsConnected { get; set; } = true;
        public List<ItemValueResult[]> SentData { get; } = new List<ItemValueResult[]>();

        public event EventHandler<bool> ConnectionStatusChanged;

        public Task<bool> InitializeAsync()
        {
            IsConnected = true;
            ConnectionStatusChanged?.Invoke(this, true);
            return Task.FromResult(true);
        }

        public Task<bool> SendDataAsync(ItemValueResult[] data)
        {
            SentData.Add(data);
            return Task.FromResult(true);
        }

        public Task<bool> StopAsync()
        {
            IsConnected = false;
            ConnectionStatusChanged?.Invoke(this, false);
            return Task.FromResult(true);
        }

        public ProtocolStatistics GetStatistics()
        {
            return new ProtocolStatistics
            {
                TotalAdapters = 3,
                EnabledAdapters = 3,
                ConnectedAdapters = 3,
                IsInitialized = true
            };
        }
    }

    /// <summary>
    /// 模拟监控服务
    /// </summary>
    public class MockMonitoringService : MonitoringService
    {
        public bool IsRunning { get; set; } = false;
        public Dictionary<string, object> Metrics { get; } = new Dictionary<string, object>();

        public override void Start()
        {
            IsRunning = true;
        }

        public override void Stop()
        {
            IsRunning = false;
        }

        public override void UpdateMetric(string name, object value, string unit)
        {
            Metrics[name] = value;
        }

        public override HealthReport GetHealthReport()
        {
            return new HealthReport
            {
                OverallStatus = HealthStatusType.Healthy,
                Timestamp = DateTime.Now,
                Components = new List<HealthStatus>
                {
                    new HealthStatus
                    {
                        ComponentName = "test",
                        Status = HealthStatusType.Healthy,
                        Timestamp = DateTime.Now
                    }
                }
            };
        }
    }

    #endregion
}
