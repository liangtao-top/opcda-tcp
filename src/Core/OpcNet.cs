using Opc;
using Opc.Da;
using OpcDAToMSA.Utils;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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
                bool isLocalConnection = IsLocalConnection(config.Opcda.Host);
                
                LoggerUtil.log.Information($"正在连接OPC服务器: {config.Opcda.Host}/{config.Opcda.Node} ({(isLocalConnection ? "本地" : "远程")}模式)");
                
                // 检查本地OPC服务器是否可用
                if (isLocalConnection)
                {
                    if (!CheckLocalOpcServerAvailable(config.Opcda.Node))
                    {
                        LoggerUtil.log.Error($"本地OPC服务器 '{config.Opcda.Node}' 不可用");
                        LoggerUtil.log.Error("请检查以下项目:");
                        LoggerUtil.log.Error("1. 是否安装了OPC服务器软件");
                        LoggerUtil.log.Error("2. OPC服务器是否正在运行");
                        LoggerUtil.log.Error("3. 服务器名称是否正确");
                        LoggerUtil.log.Error("4. 建议安装 Matrikon OPC Simulation Server 进行测试");
                        return Task.FromResult(false);
                    }
                }
                
                // 构建OPC URL，支持OPC 2.0版本
                var url = new URL($"opcda://{config.Opcda.Host}/{config.Opcda.Node}");
                
                // 根据连接类型配置连接数据
                ConnectData connectData = null;
                if (!isLocalConnection && !string.IsNullOrEmpty(config.Opcda.Username))
                {
                    var credential = new NetworkCredential(config.Opcda.Username, config.Opcda.Password);
                    connectData = new ConnectData(credential, null);
                    LoggerUtil.log.Information($"使用远程连接认证: {config.Opcda.Username}");
                }
                else if (isLocalConnection)
                {
                    LoggerUtil.log.Information("使用本地连接，无需认证");
                }
                
                LoggerUtil.log.Information($"创建OPC服务器实例...");
                server = fact.CreateInstance(url, connectData) as Opc.Da.Server;
                
                if (server == null)
                {
                    LoggerUtil.log.Error("无法创建OPC服务器实例");
                    if (isLocalConnection)
                    {
                        LoggerUtil.log.Error("本地连接失败，请检查:");
                        LoggerUtil.log.Error("1. OPC服务器是否已安装");
                        LoggerUtil.log.Error("2. OPC服务器是否正在运行");
                        LoggerUtil.log.Error("3. 服务器名称是否正确");
                        LoggerUtil.log.Error("4. 建议使用 Matrikon.OPC.Simulation.1 进行测试");
                    }
                    else
                    {
                        LoggerUtil.log.Error("远程连接失败，请检查DCOM配置");
                    }
                    return Task.FromResult(false);
                }
                
                LoggerUtil.log.Information("正在连接OPC服务器...");
                server.Connect();

                if (server.IsConnected)
                {
                    LoggerUtil.log.Information("OPC服务器连接成功，正在浏览项目...");
                    BrowsePosition position;
                    var browseElements = server.Browse(new ItemIdentifier(), filters, out position);
                    items = browseElements?.Select(be => new Item(new ItemIdentifier(be.Name))).ToList() ?? new List<Item>();
                    SetFilterItems();
                    OnConnectionStatusChanged(true);
                    LoggerUtil.log.Information($@"Opc Server {config.Opcda.Host} {config.Opcda.Node} is connected ({(isLocalConnection ? "本地" : "远程")}模式)");
                    _ = customHttpClient.PostAsync("http://localhost:31137/ui-events", new MemoryStream(Encoding.UTF8.GetBytes("{\"Event\":\"OpcDA\",\"Data\":\"运行\"}")));
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
            catch (Opc.ConnectFailedException ex)
            {
                LoggerUtil.log.Error(ex, $"OPC DA连接失败 - 连接错误: {ex.Message}");
                var config = configurationService.GetConfiguration();
                bool isLocalConnection = IsLocalConnection(config.Opcda.Host);
                
                if (isLocalConnection)
                {
                    LoggerUtil.log.Error("本地OPC服务器连接失败，请检查:");
                    LoggerUtil.log.Error("1. OPC服务器是否已安装并运行");
                    LoggerUtil.log.Error("2. 服务器名称是否正确");
                    LoggerUtil.log.Error("3. 建议安装 Matrikon OPC Simulation Server");
                    LoggerUtil.log.Error("4. 或者使用 Matrikon.OPC.Simulation.1 作为测试服务器");
                }
                else
                {
                    LoggerUtil.log.Error("远程OPC服务器连接失败，请检查:");
                    LoggerUtil.log.Error("1. OPC服务器是否正在运行");
                    LoggerUtil.log.Error("2. DCOM配置是否正确");
                    LoggerUtil.log.Error("3. 网络连接是否正常");
                }
                OnConnectionStatusChanged(false);
                return Task.FromResult(false);
            }
            catch (System.Runtime.InteropServices.ExternalException ex)
            {
                LoggerUtil.log.Error(ex, $"OPC DA连接失败 - DCOM错误: {ex.Message}");
                var config = configurationService.GetConfiguration();
                bool isLocalConnection = IsLocalConnection(config.Opcda.Host);
                
                if (isLocalConnection)
                {
                    LoggerUtil.log.Error("本地连接失败，请检查:");
                    LoggerUtil.log.Error("1. OPC服务器是否已安装并运行");
                    LoggerUtil.log.Error("2. 服务器名称是否正确");
                    LoggerUtil.log.Error("3. 是否有足够的权限");
                }
                else
                {
                    LoggerUtil.log.Error("远程连接失败，请检查:");
                    LoggerUtil.log.Error("1. OPC服务器是否正在运行");
                    LoggerUtil.log.Error("2. DCOM配置是否正确");
                    LoggerUtil.log.Error("3. 网络连接是否正常");
                    LoggerUtil.log.Error("4. 防火墙是否阻止连接");
                }
                OnConnectionStatusChanged(false);
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "OPC DA 连接失败");
                OnConnectionStatusChanged(false);
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 检查本地OPC服务器是否可用
        /// </summary>
        /// <param name="serverName">服务器名称</param>
        /// <returns>是否可用</returns>
        private bool CheckLocalOpcServerAvailable(string serverName)
        {
            try
            {
                LoggerUtil.log.Information($"检查本地OPC服务器: {serverName}");
                
                // 尝试多种连接方式来诊断问题
                var testUrls = new[]
                {
                    $"opcda://localhost/{serverName}",
                    $"opcda://127.0.0.1/{serverName}",
                    $"opcda://./{serverName}",
                    $"opcda://{Environment.MachineName}/{serverName}"
                };
                
                foreach (var testUrl in testUrls)
                {
                    try
                    {
                        LoggerUtil.log.Information($"尝试连接: {testUrl}");
                        var url = new URL(testUrl);
                        var testServer = fact.CreateInstance(url, null) as Opc.Da.Server;
                        if (testServer != null)
                        {
                            LoggerUtil.log.Information($"✅ 服务器 '{serverName}' 在 {testUrl} 可用");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.log.Debug($"❌ {testUrl} 连接失败: {ex.Message}");
                    }
                }
                
                // 如果所有URL都失败，提供详细的诊断信息
                LoggerUtil.log.Warning($"❌ 所有连接方式都失败，服务器 '{serverName}' 不可用");
                LoggerUtil.log.Information("🔍 诊断建议:");
                LoggerUtil.log.Information("1. 确认OPC Client使用的确切服务器名称");
                LoggerUtil.log.Information("2. 检查OPC Client的连接参数");
                LoggerUtil.log.Information("3. 尝试以下常见的OPC服务器名称:");
                LoggerUtil.log.Information("   - Matrikon.OPC.Simulation.1");
                LoggerUtil.log.Information("   - Kepware.KEPServerEX.V6");
                LoggerUtil.log.Information("   - OPC.SimaticNET");
                LoggerUtil.log.Information("   - RSLinx OPC Server");
                
                return false;
                }
                catch (Exception ex)
                {
                LoggerUtil.log.Error(ex, "检查OPC服务器可用性时发生错误");
                return false;
            }
        }

        /// <summary>
        /// 判断是否为本地连接
        /// </summary>
        /// <param name="host">主机地址</param>
        /// <returns>是否为本地连接</returns>
        private bool IsLocalConnection(string host)
        {
            if (string.IsNullOrEmpty(host))
                return true;
                
            var localHosts = new[] { "localhost", "127.0.0.1", ".", "本机", "local" };
            var computerName = Environment.MachineName;
            
            return localHosts.Contains(host.ToLower()) || 
                   host.Equals(computerName, StringComparison.OrdinalIgnoreCase) ||
                   host.Equals(".");
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