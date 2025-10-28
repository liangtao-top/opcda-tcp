using Newtonsoft.Json;
using Opc.Da;
using OpcDAToMSA.utils;
using OpcDAToMSA;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace OpcDAToMSA.Protocols
{
    /// <summary>
    /// 简化的 MQTT 协议适配器（不依赖 MQTTnet）
    /// </summary>
    public class MqttAdapter : IProtocolAdapter
    {
        public string ProtocolName => "MQTT";
        public bool IsEnabled { get; set; }
        public bool IsConnected => tcpClient?.Connected ?? false;

        private TcpClient tcpClient;
        private CfgJson config;
        private Dictionary<string, object> mqttSettings;
        private string dataTopic;
        private string statusTopic;
        private int qosLevel;
        private bool retainMessages;

        public MqttAdapter()
        {
            this.config = Config.GetConfig();
            this.IsEnabled = true;
        }

        /// <summary>
        /// 初始化 MQTT 适配器
        /// </summary>
        /// <returns>初始化是否成功</returns>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                // 从配置中获取 MQTT 设置
                if (config.Protocols != null && config.Protocols.ContainsKey("mqtt"))
                {
                    var mqttConfig = config.Protocols["mqtt"];
                    this.IsEnabled = mqttConfig.Enabled;
                    this.mqttSettings = mqttConfig.Settings;
                }
                else
                {
                    LoggerUtil.log.Warning("未找到 MQTT 协议配置，使用默认设置");
                    this.mqttSettings = new Dictionary<string, object>
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
                        ["retain"] = false
                    };
                }

                if (!this.IsEnabled)
                {
                    LoggerUtil.log.Information("MQTT 协议已禁用");
                    return true;
                }

                // 解析 MQTT 配置
                ParseMqttSettings();

                // 创建 TCP 客户端
                this.tcpClient = new TcpClient();

                // 解析 Broker 地址和端口
                var brokerUrl = mqttSettings.ContainsKey("broker") ? mqttSettings["broker"].ToString() : "mqtt://localhost:1883";
                var host = brokerUrl.Replace("mqtt://", "").Replace("mqtts://", "");
                var port = brokerUrl.StartsWith("mqtts://") ? 8883 : 1883;

                // 连接到 MQTT Broker
                await tcpClient.ConnectAsync(host, port);

                LoggerUtil.log.Information($"MQTT 适配器连接成功：{host}:{port}");
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

                // 创建简单的 MQTT 消息（简化版本）
                var message = CreateSimpleMqttMessage(dataTopic, jsonData);

                // 发送消息
                var stream = tcpClient.GetStream();
                await stream.WriteAsync(message, 0, message.Length);

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
        public Task<bool> DisconnectAsync()
        {
            try
            {
                if (tcpClient != null && tcpClient.Connected)
                {
                    tcpClient.Close();
                }

                tcpClient?.Dispose();
                tcpClient = null;

                LoggerUtil.log.Information("MQTT 适配器已断开连接");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "MQTT 适配器断开连接失败");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 解析 MQTT 配置设置
        /// </summary>
        private void ParseMqttSettings()
        {
            try
            {
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

                LoggerUtil.log.Debug($"MQTT 配置解析完成 - 数据主题：{dataTopic}，QoS：{qosLevel}，保留：{retainMessages}");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "MQTT 配置解析失败，使用默认值");
                dataTopic = "opcda/data";
                statusTopic = "opcda/status";
                qosLevel = 1;
                retainMessages = false;
            }
        }

        /// <summary>
        /// 创建简单的 MQTT 消息（简化版本）
        /// </summary>
        /// <param name="topic">主题</param>
        /// <param name="payload">载荷</param>
        /// <returns>消息字节数组</returns>
        private byte[] CreateSimpleMqttMessage(string topic, string payload)
        {
            // 这是一个简化的 MQTT 消息实现
            // 在实际应用中，您可能需要实现完整的 MQTT 协议
            
            var topicBytes = Encoding.UTF8.GetBytes(topic);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            
            // 简化的消息格式：[长度][主题][载荷]
            var message = new List<byte>();
            
            // 添加主题长度和主题
            message.AddRange(BitConverter.GetBytes((ushort)topicBytes.Length));
            message.AddRange(topicBytes);
            
            // 添加载荷
            message.AddRange(payloadBytes);
            
            return message.ToArray();
        }

        /// <summary>
        /// 将 OPC DA 数据转换为 MQTT 格式
        /// </summary>
        /// <param name="data">OPC DA 数据</param>
        /// <returns>MQTT 数据格式</returns>
        private object ConvertToMqttFormat(ItemValueResult[] data)
        {
            var dataPoints = new List<object>();

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
                deviceId = mqttSettings.ContainsKey("clientId") ? mqttSettings["clientId"].ToString() : "opcda-gateway",
                dataPoints = dataPoints
            };
        }
    }
}