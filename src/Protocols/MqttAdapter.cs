using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using Opc.Da;
using OpcDAToMSA.Utils;
using OpcDAToMSA.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OpcDAToMSA.Protocols
{
    /// <summary>
    /// 基于MQTTnet 4.3.7.1207的专业MQTT协议适配器
    /// </summary>
    public class MqttAdapter : IProtocolAdapter
    {
        #region Properties

        /// <summary>
        /// 协议名称
        /// </summary>
        public string ProtocolName => "MQTT";

        /// <summary>
        /// 是否启用该协议
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public bool IsConnected => mqttClient?.IsConnected ?? false;

        #endregion

        #region Private Fields

        private IMqttClient mqttClient;
        private readonly IConfigurationService configurationService;
        private Dictionary<string, object> mqttSettings;
        private string dataTopic;
        private string statusTopic;
        private int qosLevel;
        private bool retainMessages;
        private string clientId;
        private string username;
        private string password;
        private string brokerHost;
        private int brokerPort;
        private bool useTls;

        #endregion

        #region Constructor

        /// <summary>
        /// 初始化 MQTT 适配器
        /// </summary>
        public MqttAdapter(IConfigurationService configurationService)
        {
            this.configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            this.IsEnabled = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 初始化 MQTT 适配器
        /// </summary>
        /// <returns>初始化是否成功</returns>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                // 从配置中获取 MQTT 设置
                var config = configurationService.GetConfiguration();
                if (config.Protocols != null && config.Protocols.ContainsKey("mqtt"))
                {
                    var mqttConfig = config.Protocols["mqtt"];
                    this.IsEnabled = mqttConfig.Enabled;
                    this.mqttSettings = mqttConfig.Settings;
                }
                else
                {
                    LoggerUtil.log.Warning("未找到 MQTT 协议配置，使用默认设置");
                    this.mqttSettings = GetDefaultMqttSettings();
                }

                if (!this.IsEnabled)
                {
                    LoggerUtil.log.Information("MQTT 协议已禁用");
                    return true;
                }

                // 解析 MQTT 配置
                ParseMqttSettings();

                // 创建 MQTT 客户端
                var factory = new MqttFactory();
                this.mqttClient = factory.CreateMqttClient();

                // 配置客户端选项
                var options = CreateMqttClientOptions();

                // 连接到 MQTT Broker
                await mqttClient.ConnectAsync(options);

                LoggerUtil.log.Information($"MQTT 适配器连接成功：{brokerHost}:{brokerPort}");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "MQTT 适配器初始化失败");
                return false;
            }
        }

        /// <summary>
        /// 发送数据到 MQTT Broker
        /// </summary>
        /// <param name="data">OPC DA 数据</param>
        /// <returns>发送是否成功</returns>
        public async Task<bool> SendDataAsync(ItemValueResult[] data)
        {
            try
            {
                if (!this.IsEnabled || !this.IsConnected)
                {
                    return false;
                }

                // 转换数据格式
                var mqttData = ConvertToMqttFormat(data);

                // 序列化为 JSON
                var jsonData = JsonConvert.SerializeObject(mqttData, Formatting.None);

                // 创建 MQTT 消息
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(dataTopic)
                    .WithPayload(jsonData)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qosLevel)
                    .WithRetainFlag(retainMessages)
                    .Build();

                // 发送消息
                await mqttClient.PublishAsync(message);

                LoggerUtil.log.Debug($"MQTT 数据发送成功，主题：{dataTopic}，数据点：{data.Length}");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "MQTT 数据发送失败");
                return false;
            }
        }

        /// <summary>
        /// 断开 MQTT 连接
        /// </summary>
        /// <returns>断开是否成功</returns>
        public async Task<bool> DisconnectAsync()
        {
            try
            {
                if (mqttClient != null && mqttClient.IsConnected)
                {
                    await mqttClient.DisconnectAsync();
                }

                mqttClient?.Dispose();
                mqttClient = null;

                LoggerUtil.log.Information("MQTT 适配器已断开连接");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "MQTT 适配器断开连接失败");
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 获取默认 MQTT 设置
        /// </summary>
        /// <returns>默认设置字典</returns>
        private Dictionary<string, object> GetDefaultMqttSettings()
        {
            return new Dictionary<string, object>
            {
                ["broker"] = "mqtt://localhost:1883",
                ["clientId"] = "opcda-gateway",
                ["username"] = "",
                ["password"] = "",
                ["topics"] = new Dictionary<string, string>
                {
                    ["data"] = "opcda/data",
                    ["status"] = "opcda/status"
                },
                ["qos"] = 1,
                ["retain"] = false,
                ["cleanSession"] = true,
                ["keepAlive"] = 60
            };
        }

        /// <summary>
        /// 解析 MQTT 配置设置
        /// </summary>
        private void ParseMqttSettings()
        {
            try
            {
                // 解析 Broker 地址和端口
                var brokerUrl = mqttSettings.ContainsKey("broker") ? mqttSettings["broker"].ToString() : "mqtt://localhost:1883";
                
                if (brokerUrl.StartsWith("mqtts://"))
                {
                    var url = brokerUrl.Substring(8); // 移除 "mqtts://"
                    var parts = url.Split(':');
                    brokerHost = parts[0];
                    brokerPort = parts.Length > 1 ? int.Parse(parts[1]) : 8883;
                    useTls = true;
                }
                else if (brokerUrl.StartsWith("mqtt://"))
                {
                    var url = brokerUrl.Substring(7); // 移除 "mqtt://"
                    var parts = url.Split(':');
                    brokerHost = parts[0];
                    brokerPort = parts.Length > 1 ? int.Parse(parts[1]) : 1883;
                    useTls = false;
                }
                else
                {
                    brokerHost = "localhost";
                    brokerPort = 1883;
                    useTls = false;
                }

                // 解析客户端ID
                clientId = mqttSettings.ContainsKey("clientId") ? mqttSettings["clientId"].ToString() : "opcda-gateway";

                // 解析认证信息
                username = mqttSettings.ContainsKey("username") ? mqttSettings["username"].ToString() : "";
                password = mqttSettings.ContainsKey("password") ? mqttSettings["password"].ToString() : "";

                // 解析主题配置
                if (mqttSettings.ContainsKey("topics") && mqttSettings["topics"] is Dictionary<string, object> topics)
                {
                    dataTopic = topics.ContainsKey("data") ? topics["data"].ToString() : "opcda/data";
                    statusTopic = topics.ContainsKey("status") ? topics["status"].ToString() : "opcda/status";
                }
                else
                {
                    dataTopic = "opcda/data";
                    statusTopic = "opcda/status";
                }

                // 解析 QoS 级别
                qosLevel = mqttSettings.ContainsKey("qos") ? Convert.ToInt32(mqttSettings["qos"]) : 1;

                // 解析保留标志
                retainMessages = mqttSettings.ContainsKey("retain") ? Convert.ToBoolean(mqttSettings["retain"]) : false;

                LoggerUtil.log.Debug($"MQTT 配置解析完成 - Broker: {brokerHost}:{brokerPort}, 客户端ID: {clientId}, 数据主题: {dataTopic}, QoS: {qosLevel}, 保留: {retainMessages}");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "MQTT 配置解析失败，使用默认值");
                brokerHost = "localhost";
                brokerPort = 1883;
                useTls = false;
                clientId = "opcda-gateway";
                username = "";
                password = "";
                dataTopic = "opcda/data";
                statusTopic = "opcda/status";
                qosLevel = 1;
                retainMessages = false;
            }
        }

        /// <summary>
        /// 创建 MQTT 客户端选项
        /// </summary>
        /// <returns>MQTT 客户端选项</returns>
        private MqttClientOptions CreateMqttClientOptions()
        {
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithClientId(clientId)
                .WithTcpServer(brokerHost, brokerPort)
                .WithCleanSession(true)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(60));

            // 设置认证信息
            if (!string.IsNullOrEmpty(username))
            {
                optionsBuilder.WithCredentials(username, password);
            }

            // 设置 TLS（如果需要）
            if (useTls)
            {
                optionsBuilder.WithTlsOptions(options => { });
            }

            return optionsBuilder.Build();
        }

        /// <summary>
        /// 将 OPC DA 数据转换为 MQTT 格式
        /// </summary>
        /// <param name="data">OPC DA 数据</param>
        /// <returns>MQTT 数据格式</returns>
        private object ConvertToMqttFormat(ItemValueResult[] data)
        {
            var dataPoints = new List<object>();
            var config = configurationService.GetConfiguration();

            foreach (var item in data)
            {
                if (item != null)
                {
                    // 检查是否在注册表中
                    if (config.Registers.ContainsKey(item.ItemName))
                    {
                        var registerCode = config.Registers[item.ItemName];
                        dataPoints.Add(new
                        {
                            tag = item.ItemName,
                            code = registerCode,
                            value = item.Value,
                            quality = item.Quality.ToString(),
                            timestamp = item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }
                    else
                    {
                        // 未注册的数据点也发送，但标记为未注册
                        dataPoints.Add(new
                        {
                            tag = item.ItemName,
                            code = "UNREGISTERED",
                            value = item.Value,
                            quality = item.Quality.ToString(),
                            timestamp = item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }
                }
            }

            return new
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                deviceId = clientId,
                dataPoints = dataPoints
            };
        }

        #endregion
    }
}