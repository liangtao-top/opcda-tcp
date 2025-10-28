using Opc.Da;
using System.Threading.Tasks;

namespace OpcDAToMSA.Protocols
{
    /// <summary>
    /// 协议适配器接口，定义所有协议适配器的通用行为
    /// </summary>
    public interface IProtocolAdapter
    {
        /// <summary>
        /// 协议名称
        /// </summary>
        string ProtocolName { get; }

        /// <summary>
        /// 是否启用该协议
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// 获取连接状态
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 初始化协议适配器
        /// </summary>
        /// <returns>初始化是否成功</returns>
        Task<bool> InitializeAsync();

        /// <summary>
        /// 发送数据到目标协议
        /// </summary>
        /// <param name="data">OPC DA 数据</param>
        /// <returns>发送是否成功</returns>
        Task<bool> SendDataAsync(ItemValueResult[] data);

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <returns>断开是否成功</returns>
        Task<bool> DisconnectAsync();
    }
}