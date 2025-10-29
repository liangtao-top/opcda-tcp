using Opc;
using Opc.Da;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Services;
using OpcDAToMSA.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;



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
        private URL discoveredServerUrl = null;

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
                URL url;
                if (isLocalConnection)
                {
                    if (!GetLocalServers(config.Opcda.Node))
                    {
                        LoggerUtil.log.Error($"本地OPC服务器 '{config.Opcda.Node}' 不可用");
                        LoggerUtil.log.Error("请检查以下项目:");
                        LoggerUtil.log.Error("1. 是否安装了OPC服务器软件");
                        LoggerUtil.log.Error("2. OPC服务器是否正在运行");
                        LoggerUtil.log.Error("3. 服务器名称是否正确");
                        LoggerUtil.log.Error("4. 建议安装 Matrikon OPC Simulation Server 进行测试");
                        return Task.FromResult(false);
                    }
                    
                    // 使用发现的服务器URL而不是构建的URL
                    if (discoveredServerUrl == null)
                    {
                        LoggerUtil.log.Error("未发现服务器URL，无法连接");
                        return Task.FromResult(false);
                    }
                    url = discoveredServerUrl;
                    LoggerUtil.log.Information($"使用发现的服务器URL: {discoveredServerUrl}");
                }
                else
                {
                    // 远程连接使用构建的URL
                    url = new URL($"opcda://{config.Opcda.Host}/{config.Opcda.Node}");
                    LoggerUtil.log.Information($"使用构建的服务器URL: opcda://{config.Opcda.Host}/{config.Opcda.Node}");
                }
                
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
                LoggerUtil.log.Information($"URL: {url}");
                LoggerUtil.log.Information($"ConnectData: {(connectData != null ? "有认证" : "无认证")}");
                
                // 使用RR-OpcNetApi的正确方法创建服务器实例
                try
                {
                    LoggerUtil.log.Information("正在创建OPC服务器实例...");
                    server = fact.CreateInstance(url, connectData) as Opc.Da.Server;
                    LoggerUtil.log.Information("OPC服务器实例创建成功");
                }
                catch (System.Runtime.InteropServices.ExternalException ex)
                {
                    LoggerUtil.log.Error(ex, "COM组件创建失败");
                    LoggerUtil.log.Error("可能的原因:");
                    LoggerUtil.log.Error("1. OPC服务器进程未运行");
                    LoggerUtil.log.Error("2. DCOM权限配置问题");
                    LoggerUtil.log.Error("3. 需要以管理员身份运行");
                    LoggerUtil.log.Error("4. OPC服务器注册问题");
                    
                    // 尝试诊断服务器状态
                    DiagnoseOpcServerStatus();
                    throw;
                }
                
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

        private bool GetLocalServers(string targetServerName)
        {
            try
            {
                LoggerUtil.log.Information($"正在扫描本地可用的OPC服务器...");
                var servers = discovery.GetAvailableServers(Specification.COM_DA_20);
                LoggerUtil.log.Debug("GetAvailableServers {@servers}, Length: {@Length}", servers, servers.Length);
                
                if (servers != null && servers.Length > 0)
                {
                    LoggerUtil.log.Information($"发现 {servers.Length} 个可用的OPC服务器:");
                    foreach (var server in servers)
                    {
                        LoggerUtil.log.Information($"  - {server.Name}");
                        LoggerUtil.log.Debug($"    URL: {server.Url}");
                    }
                    
                    // 检查目标服务器是否在列表中
                    var foundServer = servers.FirstOrDefault(s => 
                        s.Name.Equals(targetServerName, StringComparison.OrdinalIgnoreCase) ||
                        s.Name.ToLower().Contains(targetServerName.ToLower()) ||
                        targetServerName.ToLower().Contains(s.Name.ToLower()));
                    
                    if (foundServer != null)
                    {
                        // 保存发现的服务器URL
                        discoveredServerUrl = foundServer.Url;
                        LoggerUtil.log.Information($"✅ 找到目标服务器: {foundServer.Name}");
                        LoggerUtil.log.Information($"   实际服务器名称: {foundServer.Name}");
                        LoggerUtil.log.Information($"   服务器URL: {foundServer.Url}");
                        return true;
                    }
                    else
                    {
                        LoggerUtil.log.Warning($"❌ 未找到目标服务器 '{targetServerName}'");
                        LoggerUtil.log.Information("可用服务器列表:");
                        foreach (var server in servers)
                        {
                            LoggerUtil.log.Information($"  - {server.Name}");
                        }
                        LoggerUtil.log.Information("💡 建议: 请使用上述列表中的确切服务器名称");
                        return false;
                    }
                }
                else
                {
                    LoggerUtil.log.Error("❌ 未发现任何可用的OPC服务器");
                    LoggerUtil.log.Error("请检查:");
                    LoggerUtil.log.Error("1. OPC服务器是否已安装并运行");
                    LoggerUtil.log.Error("2. OPC服务器服务是否启动");
                    LoggerUtil.log.Error("3. 防火墙是否阻止OPC通信");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "获取本地服务器失败");
                return false;
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