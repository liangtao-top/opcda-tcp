using OpcDAToMSA.Configuration;
using OpcDAToMSA.Core;
using OpcDAToMSA.Services;
using System;

namespace OpcDAToMSA.Protocols
{
    /// <summary>
    /// 协议适配器工厂实现
    /// </summary>
    public class ProtocolAdapterFactory : IProtocolAdapterFactory
    {
        #region Private Fields

        private readonly IConfigurationService configurationService;

        #endregion

        #region Constructor

        public ProtocolAdapterFactory(IConfigurationService configurationService)
        {
            this.configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        }

        #endregion

        #region Public Methods

        public IProtocolAdapter CreateAdapter(string protocolType)
        {
            switch (protocolType.ToLower())
            {
                case "msa":
                    return new MsaAdapter(configurationService);
                case "mqtt":
                    return new MqttAdapter(configurationService);
                case "modbustcp":
                    return new ModbusTcpAdapter(configurationService);
                default:
                    throw new ArgumentException($"不支持的协议类型: {protocolType}");
            }
        }

        public string[] GetSupportedProtocols()
        {
            return new[] { "msa", "mqtt", "modbustcp" };
        }

        #endregion
    }
}
