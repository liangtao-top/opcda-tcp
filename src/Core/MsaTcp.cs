using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpcDAToMSA.Configuration;
using Opc.Da;
using Newtonsoft.Json;
using OpcDAToMSA.Utils;
using System.IO;
using Newtonsoft.Json.Linq;
using OpcDAToMSA.Properties;
using System.Security.Policy;

namespace OpcDAToMSA.Core
{
    public class MsaTcp
    {
        private Socket tcpClient;
        private readonly IConfigurationService configurationService;
        private uint uid = 0;
        private bool runing = true;
        private readonly CustomHttpClient customHttpClient = new CustomHttpClient();

        #region Constructor

        public MsaTcp(IConfigurationService configurationService)
        {
            this.configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 获取 MSA 配置
        /// </summary>
        private Dictionary<string, object> GetMsaSettings()
        {
            var cfg = configurationService.GetConfiguration();
            if (cfg.Protocols != null && cfg.Protocols.ContainsKey("msa"))
            {
                return cfg.Protocols["msa"].Settings;
            }
            else
            {
                // 使用默认设置
                return new Dictionary<string, object>
                {
                    ["ip"] = "127.0.0.1",
                    ["port"] = 31100,
                    ["mn"] = 100000000,
                    ["heartbeat"] = 5000
                };
            }
        }

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public bool IsConnected => tcpClient != null && tcpClient.Connected;

        public void Run()
        {
            this.runing = true;
            _ = customHttpClient.PostAsync("http://localhost:31137/ui-events", new MemoryStream(Encoding.UTF8.GetBytes("{\"Event\":\"MSA\",\"Data\":\"连接\"}")));
            tcpClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            var msaSettings = GetMsaSettings();
            var ip = msaSettings["ip"].ToString();
            var port = System.Convert.ToInt32(msaSettings["port"]);
            
            EndPoint ep = new IPEndPoint(IPAddress.Parse(ip), port);
            try
            {
                tcpClient.SendTimeout = 1000;
                //连接Socket
                tcpClient.Connect(ep);
                LoggerUtil.log.Information($@"MSA Server {ip}:{port} is connected");
                _ = customHttpClient.PostAsync("http://localhost:31137/ui-events", new MemoryStream(Encoding.UTF8.GetBytes("{\"Event\":\"MSA\",\"Data\":\"运行\"}")));
                Task.Run(new Action(() =>
                {
                    while (runing)
                    {
                        try
                        {
                            tcpClient?.Send(Ping());
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.log.Fatal(ex, "Ping Exception");
                            break;
                        }
                        Thread.Sleep(System.Convert.ToInt32(msaSettings["heartbeat"]));
                    }
                }));
                Task.Run(new Action(() =>
                {
                    while (runing)
                    {
                        byte[] buffer = new byte[1024 * 10];
                        try
                        {
                            int byteCount = tcpClient.Receive(buffer, tcpClient.Available, SocketFlags.None);
                            if (byteCount > 0)
                            {
                                byte[] result = new byte[byteCount];
                                Buffer.BlockCopy(buffer, 0, result, 0, byteCount);
                                Receive(result);
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.log.Fatal(ex, "Receive Exception");
                            break;
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Fatal(ex, "连接远程 MSA Server 意外终止");
                var msaSettingsRetry = GetMsaSettings();
                Thread.Sleep(System.Convert.ToInt32(msaSettingsRetry["heartbeat"]));
                Run();
            }
        }

        public void Stop() { 
            this.runing= false;
            var msaSettings = GetMsaSettings();
            var ip = msaSettings["ip"].ToString();
            var port = System.Convert.ToInt32(msaSettings["port"]);
            LoggerUtil.log.Information($@"MSA Server {ip}:{port} is stop");
            _ = customHttpClient.PostAsync("http://localhost:31137/ui-events", new MemoryStream(Encoding.UTF8.GetBytes("{\"Event\":\"MSA\",\"Data\":\"停止\"}")));
        }

        //接收数据
        private void Receive(byte[] bytes)
        {
            Msa<FrameFormat1> msa = Unpack<FrameFormat1>(bytes);
            LoggerUtil.log.Debug("Receive: " + JsonConvert.SerializeObject(msa, new JsonSerializerSettings() { Formatting = Formatting.None, NullValueHandling = NullValueHandling.Ignore }));
            FrameFormat1 frameFormat = msa.body;
            switch (frameFormat.func)
            {
                case Func.Pong:
                    uid = msa.uid;
                    break;
                case Func.DeviceNotRegistered:
                    LoggerUtil.log.Warning($@"MN：{frameFormat.gid}，{frameFormat.msg}");
                    break;
            }
        }

        public void Send(ItemValueResult[] values)
        {
            var cfg = configurationService.GetConfiguration();
            Dictionary<string, string> regs = cfg.Registers;
            Dictionary<string, object> data = new Dictionary<string, object>();
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                {
                    if (regs.ContainsKey(values[i].ItemName))
                    {
                        regs.TryGetValue(values[i].ItemName, out string key);
                        if (!String.IsNullOrEmpty(key)) {
                            try {
                                data.Add(key, values[i].Value);
                            }
                            catch (Exception e) {
                                LoggerUtil.log.Debug("key: {@key}, value: {@value}", key, values[i].Value);
                                LoggerUtil.log.Error(e.ToString());
                            }
                        }
                    }
                    else
                    {
                        LoggerUtil.log.Warning($@"指标{values[i].ItemName}未在配置项Registers注册");
                    }
                }
            }
            //LoggerUtil.log.Debug("Points: {@data}", data);
            if (data.Count > 0)
            {
                bool isSocketConnected = !IsSocketConnected(tcpClient);
                LoggerUtil.log.Debug("isSocketConnected: {@isSocketConnected}", isSocketConnected);
                if (!isSocketConnected)
                {
                    tcpClient?.Close();
                    Run();
                }
                tcpClient?.Send(Escalation(data));
            }
        }

        // 模板数据封包
        private byte[] Escalation(Dictionary<string, object> data)
        {
            var msaSettings = GetMsaSettings();
            var mn = System.Convert.ToUInt32(msaSettings["mn"]);
            
            FrameFormat2 frameFormat = new FrameFormat2()
            {
                gid = "G" + mn,
                ptid = 0,
                cid = 1,
                time = DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss"),
                func = Func.TemplateData,
                level = 103,
                consume = 0,
                err = 0,
                points = data
            };
            string body = JsonConvert.SerializeObject(frameFormat);
            //Console.WriteLine($@"Ping：{body}@{body.Length}");
            Msa<FrameFormat2> msa = new Msa<FrameFormat2>()
            {
                type = Encoding.UTF8.GetBytes("N")[0],//N代表无符号、网络字节序、4 字节
                uid = uid,
                length = (uint)body.Length,
                serid = mn,
                body = frameFormat
            };
            LoggerUtil.log.Information("Escalation: \r\n" + JsonConvert.SerializeObject(msa, new JsonSerializerSettings() { Formatting = Formatting.Indented }));
            return Packet(msa);
        }

        private byte[] Ping()
        {
            var msaSettings = GetMsaSettings();
            var mn = System.Convert.ToUInt32(msaSettings["mn"]);
            
            FrameFormat0 frameFormat = new FrameFormat0()
            {
                gid = "G" + mn,
                ptid = 0,
                cid = 1,
                time = DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss"),
                func = Func.Ping
            };
            string body = JsonConvert.SerializeObject(frameFormat);
            //Console.WriteLine($@"Ping：{body}@{body.Length}");
            Msa<FrameFormat0> msa = new Msa<FrameFormat0>()
            {
                type = Encoding.UTF8.GetBytes("N")[0],//N代表无符号、网络字节序、4 字节
                uid = uid,
                length = (uint)body.Length,
                serid = mn,
                body = frameFormat
            };
            LoggerUtil.log.Debug("Ping: " + JsonConvert.SerializeObject(msa));
            return Packet(msa);
        }

        private byte[] Packet<T>(Msa<T> msa)
        {
            //Console.WriteLine(JsonConvert.SerializeObject(msa));
            byte[] buffer = new byte[msa.length + 16];
            byte[] type = BitConverter.GetBytes(msa.type);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(type);
            Array.Copy(type, 0, buffer, 0, type.Length);
            byte[] uid = BitConverter.GetBytes(msa.uid);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(uid);
            Array.Copy(uid, 0, buffer, 4, uid.Length);
            byte[] length = BitConverter.GetBytes(msa.length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(length);
            Array.Copy(length, 0, buffer, 8, length.Length);
            byte[] serid = BitConverter.GetBytes(msa.serid);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(serid);
            Array.Copy(serid, 0, buffer, 12, serid.Length);
            byte[] body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(msa.body));
            Array.Copy(body, 0, buffer, 16, body.Length);
            //Console.WriteLine($@"{BitConverter.ToString(buffer)}@{buffer.Length}");
            return buffer;
        }

        private Msa<T> Unpack<T>(byte[] bytes)
        {
            byte[] type = bytes.Skip(0).Take(4).ToArray();
            //Console.WriteLine($@"{BitConverter.ToString(type)}@{type.Length}");
            byte[] uid = bytes.Skip(4).Take(4).ToArray();
            //Console.WriteLine($@"{BitConverter.ToString(uid)}@{uid.Length}");
            byte[] length = bytes.Skip(8).Take(4).ToArray();
            //Console.WriteLine($@"{BitConverter.ToString(length)}@{length.Length}");
            byte[] serid = bytes.Skip(12).Take(4).ToArray();
            //Console.WriteLine($@"{BitConverter.ToString(serid)}@{serid.Length}");
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(uid);
                Array.Reverse(length);
                Array.Reverse(serid);
            }
            int byteCount = BitConverter.ToInt32(length, 0);
            byte[] body = bytes.Skip(16).Take(byteCount).ToArray();
            //Console.WriteLine($@"{Encoding.UTF8.GetString(body)}@{body.Length}");
            Msa<T> msa = new Msa<T>()
            {
                type = BitConverter.ToUInt32(type, 0),
                uid = BitConverter.ToUInt32(uid, 0),
                length = BitConverter.ToUInt32(length, 0),
                serid = BitConverter.ToUInt32(serid, 0),
                body = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(body)),
            };
            return msa;
        }

        // 检查一个Socket是否可连接
        private bool IsSocketConnected(Socket client)
        {
            bool blockingState = client.Blocking;
            try
            {
                byte[] tmp = new byte[1];
                client.Blocking = false;
                client?.Send(tmp, 0, 0);
                return false;
            }
            catch (SocketException e)
            {
                // 产生 10035 == WSAEWOULDBLOCK 错误，说明被阻止了，但是还是连接的
                if (e.NativeErrorCode.Equals(10035))
                    return false;
                else
                    return true;
            }
            finally
            {
                client.Blocking = blockingState;    // 恢复状态
            }
        }

    }

    // MSA协议 包头长度为 4 个整型，16 字节，length 长度值在第 3 个整型处。因此 package_length_offset 设置为 8，0-3 字节为 type，4-7 字节为 uid，8-11 字节为 length，12-15 字节为 serid。
    struct Msa<T>
    {
        public uint type;
        public uint uid;
        public uint length;
        public uint serid;
        public T body;
    };

    // Ping帧格式
    struct FrameFormat0
    {
        // 网关ID，唯一标识网关出厂 ID
        public string gid;
        // 通信点表ID ，用于记录点表文件
        public uint ptid;
        // 通道ID，绑 定 到 网 关 的 设 备 号 [1/2/3 ]
        public uint cid;
        // 实时时间 "2017/09/04 08:46:40" 
        public string time;
        // 功能码
        public Func func;
    };

    // 服务器返回状态帧格式
    #pragma warning disable 649
    struct FrameFormat1
    {
        // 网关ID，唯一标识网关出厂 ID
        public string gid;
        // 通信点表ID ，用于记录点表文件
        public uint ptid;
        // 通道ID，绑 定 到 网 关 的 设 备 号 [1/2/3 ]
        public uint cid;
        // 实时时间 "2017/09/04 08:46:40" 
        public string time;
        // 功能码
        public Func func;
        // 消息提示
        public string msg;
    }
    // 数据上报帧格式
    struct FrameFormat2
    {
        // 网关ID，唯一标识网关出厂 ID
        public string gid;
        // 通信点表ID ，用于记录点表文件
        public uint ptid;
        // 通道ID，绑 定 到 网 关 的 设 备 号 [1/2/3 ]
        public uint cid;
        // 实时时间 "2017/09/04 08:46:40" 
        public string time;
        // 功能码
        public Func func;
        // 采集耗时，网关根据点表采集 PLC 所消耗的时间，单位 ms
        public uint consume;
        // 错误代码，0 正常 其他读取失败
        public int err;
        // 数据模式
        // 实时数据/状态 100
        // 历史数据/状态：101
        // 变传数据/状态：102
        // 模板数据/状态：103
        public int level;
        // 点位数据
        public Dictionary<string, object> points;
    };

    // 功能码
    enum Func
    {
        // 心跳请求帧 1 Client->Server 维持网关与服务器连接
        Ping = 1,
        // 实时数据帧 2 Client->Server 采集正常数据内容
        RealData = 2,
        // 实时状态帧 3 Client->Server 采集出错信息
        RealState = 3,
        // 远程控制回应帧 4 Client->Server 网关接收远程控制命令处理后回应
        RemoteRev = 4,
        // AGPS 定位帧 5 Client->Server 基站定位信息(仅 4G 型号支持)
        AGPS = 5,
        // 历史数据帧 6 Client->Server 网关离线后存储的设备数据
        HistoryData = 6,
        // 历史状态帧 7 Client->Server 网关离线后存储的采集出错信息
        HistoryState = 7,
        // 变传数据帧 8 Client->Server 采集数据变化后立刻推送的数据
        ChangeData = 7,
        // 变传状态帧 9 Client->Server 采集变化后立刻推送的错误信息
        ChangeState = 7,
        // 模板数据帧 10 Client->Server 根据变传周期定时推送的实时数据
        TemplateData = 10,
        // 模板状态帧 11 Client->Server 根据变传周期定时推送的错误状态
        TemplateState = 11,
        // 心跳回应帧 81 Server->Client 维持网关与服务器连接
        Pong = 81,
        // 远程控制请求帧 83 Server->Client 服务器远程控制设备请
        RemoteReq = 83,
        // 设备未注册返回帧
        DeviceNotRegistered = 101,
    };

    #endregion
}
