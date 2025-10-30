using Opc.Da;
using OpcDAToMSA.Protocols;
// using OpcDAToMSA.Monitoring; // 已移除旧监测服务
using System;
using System.Threading.Tasks;

namespace OpcDAToMSA.Services
{
    /// <summary>
    /// 协议统计信息
    /// </summary>
    public class ProtocolStatistics
    {
        /// <summary>
        /// 总适配器数量
        /// </summary>
        public int TotalAdapters { get; set; }

        /// <summary>
        /// 启用的适配器数量
        /// </summary>
        public int EnabledAdapters { get; set; }

        /// <summary>
        /// 已连接的适配器数量
        /// </summary>
        public int ConnectedAdapters { get; set; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized { get; set; }
    }

    /// <summary>
    /// 服务状态
    /// </summary>
    public class ServiceStatus
    {
        public bool DataServiceRunning { get; set; }
        // 旧 MonitoringService 字段已移除
    }

    /// <summary>
    /// 数据提供者接口
    /// </summary>
    public interface IDataProvider
    {
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <returns>数据结果</returns>
        Task<ItemValueResult[]> ReadDataAsync();

        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接状态变更事件
        /// </summary>
        event EventHandler<bool> ConnectionStatusChanged;
    }

    /// <summary>
    /// 数据发送者接口
    /// </summary>
    public interface IDataSender
    {
        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns>是否成功</returns>
        Task<bool> SendDataAsync(ItemValueResult[] data);

        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接状态变更事件
        /// </summary>
        event EventHandler<bool> ConnectionStatusChanged;
    }

    /// <summary>
    /// 数据服务接口（组合接口）
    /// </summary>
    public interface IDataService : IDataProvider, IDataSender
    {
        /// <summary>
        /// 启动服务
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> StartAsync();

        /// <summary>
        /// 停止服务
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> StopAsync();

        /// <summary>
        /// 是否正在运行
        /// </summary>
        bool IsRunning { get; }
    }

    /// <summary>
    /// OPC DA 数据提供者
    /// </summary>
    public interface IOpcDataProvider : IDataProvider
    {
        /// <summary>
        /// 连接到 OPC DA 服务器
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> ConnectAsync();

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> DisconnectAsync();

        /// <summary>
        /// 获取可用服务器列表
        /// </summary>
        /// <returns>服务器列表</returns>
        string[] GetAvailableServers();
    }

    /// <summary>
    /// 协议路由器接口
    /// </summary>
    public interface IProtocolRouter : IDataSender
    {
        /// <summary>
        /// 初始化协议路由器
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> InitializeAsync();

        /// <summary>
        /// 停止协议路由器
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> StopAsync();

        /// <summary>
        /// 获取统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        ProtocolStatistics GetStatistics();
    }

    /// <summary>
    /// 协议适配器工厂接口
    /// </summary>
    public interface IProtocolAdapterFactory
    {
        /// <summary>
        /// 创建协议适配器
        /// </summary>
        /// <param name="protocolType">协议类型</param>
        /// <returns>协议适配器</returns>
        IProtocolAdapter CreateAdapter(string protocolType);

        /// <summary>
        /// 获取支持的协议类型
        /// </summary>
        /// <returns>协议类型列表</returns>
        string[] GetSupportedProtocols();
    }

    /// <summary>
    /// 服务管理器接口
    /// </summary>
    public interface IServiceManager
    {
        /// <summary>
        /// 启动所有服务
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> StartAllServicesAsync();

        /// <summary>
        /// 停止所有服务
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> StopAllServicesAsync();

        /// <summary>
        /// 获取服务状态
        /// </summary>
        /// <returns>服务状态</returns>
        ServiceStatus GetServiceStatus();

        /// <summary>
        /// 是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 获取指定类型的服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>服务实例</returns>
        T GetService<T>() where T : class;
    }
}
