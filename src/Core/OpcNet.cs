using Opc;
using Opc.Da;
using OpcDAToMSA.Utils;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq;

namespace OpcDAToMSA.Core
{
    /// <summary>
    /// OPC DA 数据提供者实现
    /// </summary>
    public class OpcNet : IOpcDataProvider
    {
        #region Private Fields

        private readonly OpcCom.Factory fact = new OpcCom.Factory();
        private Opc.Da.Server server = null;
        public List<Item> items = null;
        public List<Item> filterItems = null;
        private readonly IConfigurationService configurationService;
        private readonly IDiscovery discovery = new OpcCom.ServerEnumerator();
        private readonly BrowseFilters filters = new BrowseFilters
        {
            BrowseFilter = browseFilter.all,
            ElementNameFilter = "",
            MaxElementsReturned = 0,
            ReturnAllProperties = false,
            ReturnPropertyValues = true,
            VendorFilter = ""
        };
        private bool runing = true;
        private readonly CustomHttpClient customHttpClient = new CustomHttpClient();

        #endregion

        #region Public Properties

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning => runing;

        #endregion

        #region Events

        public event EventHandler<bool> ConnectionStatusChanged;

        #endregion

        #region Properties

        public bool IsConnected => server != null && server.IsConnected;

        #endregion

        #region Constructor

        public OpcNet(IConfigurationService configurationService)
        {
            this.configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            this.GetLocalServers();
        }

        #endregion

        #region Public Methods

        public Task<bool> ConnectAsync()
        {
            try
            {
                var config = configurationService.GetConfiguration();
                var url = new URL($"opcda://{config.Opcda.Host}/{config.Opcda.Node}");
                server = fact.CreateInstance(url, null) as Opc.Da.Server;
                server.Connect();

                if (server.IsConnected)
                {
                    BrowsePosition position;
                    var browseElements = server.Browse(new ItemIdentifier(), filters, out position);
                    items = browseElements?.Select(be => new Item(new ItemIdentifier(be.Name))).ToList() ?? new List<Item>();
                    SetFilterItems();
                    OnConnectionStatusChanged(true);
                    LoggerUtil.log.Information($@"Opc Server {config.Opcda.Host} {config.Opcda.Node} is connected");
                    _ = customHttpClient.PostAsync("http://localhost:31137/ui-events", new MemoryStream(Encoding.UTF8.GetBytes("{\"Event\":\"OpcDA\",\"Data\":\"运行\"}")));
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "OPC DA 连接失败");
                OnConnectionStatusChanged(false);
                return Task.FromResult(false);
            }
        }

        public Task<bool> DisconnectAsync()
        {
            try
            {
                if (server != null && server.IsConnected)
                {
                    server.Disconnect();
                    server = null;
                }
                OnConnectionStatusChanged(false);
                LoggerUtil.log.Information("OPC DA 连接已断开");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "断开 OPC DA 连接失败");
                return Task.FromResult(false);
            }
        }

        public Task<ItemValueResult[]> ReadDataAsync()
        {
            try
            {
                if (!IsConnected || filterItems == null || filterItems.Count == 0)
                {
                    LoggerUtil.log.Warning("OPC DA 未连接或没有过滤项");
                    return Task.FromResult(new ItemValueResult[0]);
                }

                var values = server.Read(filterItems.ToArray());
                if (values != null && values.Length > 0)
                {
                    LoggerUtil.log.Debug($"OPC DA 读取到 {values.Length} 个数据点");
                    return Task.FromResult(values);
                }

                return Task.FromResult(new ItemValueResult[0]);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "OPC DA 读取数据失败");
                return Task.FromResult(new ItemValueResult[0]);
            }
        }

        public string[] GetAvailableServers()
        {
            try
            {
                var servers = discovery.GetAvailableServers(Specification.COM_DA_20);
                LoggerUtil.log.Debug("GetAvailableServers {@servers}, Length: {@Length}", servers, servers.Length);
                return servers.Select(s => s.Name).ToArray();
                }
                catch (Exception ex)
                {
                LoggerUtil.log.Error(ex, "获取可用服务器失败");
                return new string[0];
            }
        }

        public void Stop()
        {
            this.runing = false;
            DisconnectAsync().Wait();
            var config = configurationService.GetConfiguration();
            LoggerUtil.log.Information($@"Opc Server {config.Opcda.Host} {config.Opcda.Node} is stop");
            _ = customHttpClient.PostAsync("http://localhost:31137/ui-events", new MemoryStream(Encoding.UTF8.GetBytes("{\"Event\":\"OpcDA\",\"Data\":\"停止\"}")));
        }

        #endregion

        #region Private Methods

        private void OnConnectionStatusChanged(bool isConnected)
        {
            ConnectionStatusChanged?.Invoke(this, isConnected);
        }

        private void GetLocalServers()
        {
            try
            {
                var servers = discovery.GetAvailableServers(Specification.COM_DA_20);
                LoggerUtil.log.Debug("GetAvailableServers {@servers}, Length: {@Length}", servers, servers.Length);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "获取本地服务器失败");
            }
        }

        private void SetFilterItems()
        {
            var config = configurationService.GetConfiguration();
            var regs = config.Registers;
            filterItems = new List<Item>();
            
            if (regs != null && regs.Count > 0 && items != null)
            {
                items.ForEach(item =>
                {
                    if (regs.ContainsKey(item.ItemName.ToString()))
                    {
                        filterItems.Add(item);
                    }
                });
            }
        }

        #endregion
    }
}