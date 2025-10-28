using Modbus.Data;
using Modbus.Device;
using OpcDAToMSA.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OpcDAToMSA.Core
{
    /// <summary>
    /// Modbus TCP 服务端实现
    /// </summary>
    public class ModbusTcp
    {
        private ModbusTcpSlave slave = null;
        private DataStore store = null;
        private TcpListener tcpListener = null;
        private bool isRunning = false;
        private Thread listenerThread = null;

        // 配置参数
        private string listenIp;
        private int listenPort;
        private byte stationId;

        // 寄存器映射配置
        private Dictionary<string, int> registerMapping;

        public DataStore Store { get => store; set => store = value; }
        public bool IsConnected => slave != null && isRunning;

        /// <summary>
        /// 构造函数，支持配置注入
        /// </summary>
        /// <param name="ip">监听IP地址</param>
        /// <param name="port">监听端口</param>
        /// <param name="station">站号</param>
        public ModbusTcp(string ip = "0.0.0.0", int port = 502, byte station = 1)
        {
            this.listenIp = ip;
            this.listenPort = port;
            this.stationId = station;
            this.registerMapping = new Dictionary<string, int>();
        }

        /// <summary>
        /// 启动 Modbus TCP 服务
        /// </summary>
        public void Run()
        {
            try
            {
                LoggerUtil.log.Information($"启动 Modbus TCP 服务，监听 {listenIp}:{listenPort}，站号：{stationId}");

                // 创建 TCP 监听器
                tcpListener = new TcpListener(IPAddress.Parse(listenIp), listenPort);
                
                // 创建 Modbus TCP 从站
                slave = ModbusTcpSlave.CreateTcp(stationId, tcpListener);
                slave.DataStore = store = DataStoreFactory.CreateDefaultDataStore();

                // 订阅事件
                SubscribeToEvents();

                // 启动监听线程
                isRunning = true;
                listenerThread = new Thread(() => 
                {
                    try
                    {
                        slave.ListenAsync();
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.log.Error(ex, "Modbus TCP 监听线程异常");
                    }
                })
                { IsBackground = true };
                
                listenerThread.Start();

                LoggerUtil.log.Information("Modbus TCP 服务启动成功");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "Modbus TCP 服务启动失败");
                throw;
            }
        }

        /// <summary>
        /// 停止 Modbus TCP 服务
        /// </summary>
        public void Stop()
        {
            try
            {
                LoggerUtil.log.Information("正在停止 Modbus TCP 服务...");

                isRunning = false;

                // 停止监听
                if (slave != null)
                {
                    slave.Dispose();
                    slave = null;
                }

                // 停止 TCP 监听器
                if (tcpListener != null)
                {
                    tcpListener.Stop();
                    tcpListener = null;
                }

                // 等待监听线程结束
                if (listenerThread != null && listenerThread.IsAlive)
                {
                    listenerThread.Join(5000); // 等待5秒
                    if (listenerThread.IsAlive)
                    {
                        LoggerUtil.log.Warning("Modbus TCP 监听线程未正常结束，强制终止");
                        listenerThread.Abort();
                    }
                }

                LoggerUtil.log.Information("Modbus TCP 服务已停止");
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "停止 Modbus TCP 服务时发生异常");
            }
        }

        /// <summary>
        /// 订阅 Modbus 事件
        /// </summary>
        private void SubscribeToEvents()
        {
            // 数据写入事件
            slave.DataStore.DataStoreWrittenTo += (sender, e) =>
            {
                LoggerUtil.log.Debug($"Modbus 数据写入事件 - 地址：{e.StartAddress}");
            };

            // 请求接收事件
            slave.ModbusSlaveRequestReceived += (sender, e) =>
            {
                LoggerUtil.log.Debug($"Modbus 请求接收 - 功能码：{e.Message.FunctionCode}");
            };

            // 写入完成事件
            slave.WriteComplete += (sender, e) =>
            {
                LoggerUtil.log.Debug($"Modbus 写入完成");
            };
        }

        /// <summary>
        /// 设置寄存器映射
        /// </summary>
        /// <param name="mapping">OPC标签到寄存器地址的映射</param>
        public void SetRegisterMapping(Dictionary<string, int> mapping)
        {
            this.registerMapping = mapping ?? new Dictionary<string, int>();
            LoggerUtil.log.Information($"设置 Modbus 寄存器映射，共 {registerMapping.Count} 个映射");
        }

        /// <summary>
        /// 更新寄存器数据
        /// </summary>
        /// <param name="tagName">OPC标签名</param>
        /// <param name="value">数据值</param>
        /// <returns>更新是否成功</returns>
        public bool UpdateRegister(string tagName, object value)
        {
            try
            {
                if (!registerMapping.ContainsKey(tagName))
                {
                    LoggerUtil.log.Warning($"OPC标签 {tagName} 未配置寄存器映射");
                    return false;
                }

                int registerAddress = registerMapping[tagName];
                
                // 根据数据类型写入不同的寄存器
                switch (value)
                {
                    case bool boolValue:
                        store.HoldingRegisters[registerAddress] = (ushort)(boolValue ? 1 : 0);
                        break;
                    case int intValue:
                        store.HoldingRegisters[registerAddress] = (ushort)intValue;
                        break;
                    case float floatValue:
                        // 将 float 转换为两个 16位寄存器
                        byte[] bytes = BitConverter.GetBytes(floatValue);
                        if (BitConverter.IsLittleEndian)
                        {
                            store.HoldingRegisters[registerAddress] = BitConverter.ToUInt16(bytes, 0);
                            store.HoldingRegisters[registerAddress + 1] = BitConverter.ToUInt16(bytes, 2);
                        }
                        else
                        {
                            store.HoldingRegisters[registerAddress] = BitConverter.ToUInt16(bytes, 2);
                            store.HoldingRegisters[registerAddress + 1] = BitConverter.ToUInt16(bytes, 0);
                        }
                        break;
                    case double doubleValue:
                        // 将 double 转换为四个 16位寄存器
                        byte[] doubleBytes = BitConverter.GetBytes(doubleValue);
                        for (int i = 0; i < 4; i++)
                        {
                            store.HoldingRegisters[registerAddress + i] = BitConverter.ToUInt16(doubleBytes, i * 2);
                        }
                        break;
                    default:
                        // 尝试转换为字符串
                        string stringValue = value?.ToString() ?? "";
                        byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(stringValue);
                        for (int i = 0; i < Math.Min(stringBytes.Length, 2); i++)
                        {
                            store.HoldingRegisters[registerAddress + i] = stringBytes[i];
                        }
                        break;
                }

                LoggerUtil.log.Debug($"Modbus 寄存器更新成功 - 标签：{tagName}，地址：{registerAddress}，值：{value}");
                return true;
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, $"Modbus 寄存器更新失败 - 标签：{tagName}，值：{value}");
                return false;
            }
        }

        /// <summary>
        /// 批量更新寄存器数据
        /// </summary>
        /// <param name="data">OPC DA 数据</param>
        /// <returns>更新成功的数量</returns>
        public int UpdateRegisters(Opc.Da.ItemValueResult[] data)
        {
            int successCount = 0;
            
            foreach (var item in data)
            {
                if (item != null && UpdateRegister(item.ItemName, item.Value))
                {
                    successCount++;
                }
            }

            LoggerUtil.log.Debug($"Modbus 批量更新完成 - 成功：{successCount}/{data.Length}");
            return successCount;
        }
    }
}
