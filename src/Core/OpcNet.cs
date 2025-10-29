using Opc;
using Opc.Da;
using OpcDAToMSA.Configuration;
using OpcDAToMSA.Services;
using OpcDAToMSA.Utils;
using OpcDAToMSA.Events;
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
            BrowseFilter = browseFilter.all, // 浏览所有元素
            ElementNameFilter = "",
            MaxElementsReturned = 10000, // 设置最大返回元素数量
            ReturnAllProperties = false,
            ReturnPropertyValues = true,
            VendorFilter = ""
        };
        private bool runing = true;
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
                        OnConnectionStatusChanged(false);
                        ApplicationEvents.OnOpcConnectionChanged(false, "OPC服务器不可用");
                        return Task.FromResult(false);
                    }
                    
                    // 使用发现的服务器URL而不是构建的URL
                    if (discoveredServerUrl == null)
                    {
                        LoggerUtil.log.Error("未发现服务器URL，无法连接");
                        OnConnectionStatusChanged(false);
                        ApplicationEvents.OnOpcConnectionChanged(false, "未发现服务器URL");
                        return Task.FromResult(false);
                    }
                    url = discoveredServerUrl;
                    LoggerUtil.log.Information($"使用发现的服务器URL: {discoveredServerUrl}");
                }
                else
                {
                    
                    // 远程连接使用构建的URL
                    url = new URL($@"opcda://{config.Opcda.Host}/{config.Opcda.Node}");
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
                    // 本地连接也需要ConnectData，但使用null认证
                    connectData = new ConnectData(null, null);
                    LoggerUtil.log.Information("使用本地连接，无需认证");
                }
                
                LoggerUtil.log.Information($"创建OPC服务器实例...");
                LoggerUtil.log.Information($"URL: {url}");
                LoggerUtil.log.Information($"ConnectData: {(connectData != null ? "有认证" : "无认证")}");
                
                // 使用RR-OpcNetApi的正确方法创建服务器实例
                try
                {
                    LoggerUtil.log.Information("正在创建OPC服务器实例...");
                    server = new Opc.Da.Server(fact, url);
                    //server = fact.CreateInstance(url, connectData) as Opc.Da.Server;
                    LoggerUtil.log.Information("OPC服务器实例创建成功");
                }
                catch (System.Runtime.InteropServices.ExternalException ex)
                {
                    LoggerUtil.log.Warning($"简化URL连接失败: {ex.Message}");
                    
                    // 如果是本地连接且简化URL失败，尝试使用完整URL
                    if (isLocalConnection && discoveredServerUrl != null)
                    {
                        try
                        {
                            LoggerUtil.log.Information("尝试使用完整的服务器URL...");
                            url = discoveredServerUrl;
                            server = fact.CreateInstance(url, connectData) as Opc.Da.Server;
                            LoggerUtil.log.Information("使用完整URL连接成功");
                        }
                        catch (System.Runtime.InteropServices.ExternalException ex2)
                        {
                            LoggerUtil.log.Error(ex2, "完整URL连接也失败");
                            LoggerUtil.log.Error("COM组件创建失败的可能原因:");
                            LoggerUtil.log.Error("1. OPC服务器进程未运行");
                            LoggerUtil.log.Error("2. DCOM权限配置问题");
                            LoggerUtil.log.Error("3. 需要以管理员身份运行");
                            LoggerUtil.log.Error("4. OPC服务器注册问题");
                            throw ex2;
                        }
                    }
                    else
                    {
                        LoggerUtil.log.Error(ex, "COM组件创建失败");
                        LoggerUtil.log.Error("可能的原因:");
                        LoggerUtil.log.Error("1. OPC服务器进程未运行");
                        LoggerUtil.log.Error("2. DCOM权限配置问题");
                        LoggerUtil.log.Error("3. 需要以管理员身份运行");
                        LoggerUtil.log.Error("4. OPC服务器注册问题");
                        throw;
                    }
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
                LoggerUtil.log.Information($"连接参数 - URL: {url}, ConnectData: {(connectData != null ? "有" : "无")}");
                
                // 尝试使用URL和ConnectData连接
                try
                {
                    server.Connect(url, connectData);
                    LoggerUtil.log.Information("使用URL和ConnectData连接成功");
                }
                catch (Exception ex)
                {
                    LoggerUtil.log.Warning($"使用URL和ConnectData连接失败: {ex.Message}");
                    
                    // 如果失败，尝试直接连接
                    try
                    {
                        LoggerUtil.log.Information("尝试直接连接...");
                        server.Connect();
                        LoggerUtil.log.Information("直接连接成功");
                    }
                    catch (Exception ex2)
                    {
                        LoggerUtil.log.Error($"直接连接也失败: {ex2.Message}");
                        throw ex2;
                    }
                }

                if (server.IsConnected)
                {
                    LoggerUtil.log.Information("OPC服务器连接成功，正在浏览项目...");
                    
                    // 尝试递归浏览所有标签
                    items = BrowseAllItems(new ItemIdentifier(), filters);
                    
                    LoggerUtil.log.Information($"浏览完成，共发现 {items?.Count ?? 0} 个标签");
                    
                    SetFilterItems();
                    OnConnectionStatusChanged(true);
                    LoggerUtil.log.Information($@"Opc Server {config.Opcda.Host} {config.Opcda.Node} is connected ({(isLocalConnection ? "本地" : "远程")}模式)");
                    ApplicationEvents.OnOpcConnectionChanged(true, "OPC连接成功");
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
                ApplicationEvents.OnOpcConnectionChanged(false, "OPC连接失败");
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
                ApplicationEvents.OnOpcConnectionChanged(false, "OPC连接失败");
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "OPC DA 连接失败");
                OnConnectionStatusChanged(false);
                ApplicationEvents.OnOpcConnectionChanged(false, "OPC连接失败");
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
                ApplicationEvents.OnOpcConnectionChanged(false, "OPC连接断开");
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

                // 尝试读取数据，如果失败则尝试不同的标签名称格式
                var values = server.Read(filterItems.ToArray());
                
                // 检查是否有失败的标签
                bool hasFailedItems = false;
                if (values != null && values.Length > 0)
                {
                    for (int i = 0; i < values.Length; i++)
                    {
                        if (values[i].ResultID != null && values[i].ResultID.Failed())
                        {
                            hasFailedItems = true;
                            break;
                        }
                    }
                }
                
                // 如果有失败的标签，尝试不同的名称格式
                if (hasFailedItems && values != null && values.Length > 0)
                {
                    LoggerUtil.log.Warning("检测到标签读取失败，尝试不同的标签名称格式...");
                    var alternativeItems = new List<Item>();
                    
                    foreach (var originalItem in filterItems)
                    {
                        var originalName = originalItem.ItemName.ToString();
                        
                        // 尝试不同的格式
                        var alternativeNames = new[]
                        {
                            originalName, // 原始名称
                            originalName.Replace('.', '/'), // 用 / 替换 .
                            originalName.Replace('.', '_'), // 用 _ 替换 .
                            originalName.ToUpper(), // 大写
                            originalName.ToLower(), // 小写
                            originalName.Replace("V4.", ""), // 移除 V4. 前缀
                            originalName.Replace("V4.", "V4/"), // V4. 改为 V4/
                        };
                        
                        bool found = false;
                        foreach (var altName in alternativeNames)
                        {
                            if (altName == originalName) continue; // 跳过原始名称
                            
                            try
                            {
                                var testItem = new Item(new ItemIdentifier(altName));
                                var testResult = server.Read(new Item[] { testItem });
                                
                                if (testResult != null && testResult.Length > 0 && 
                                    testResult[0].ResultID != null && !testResult[0].ResultID.Failed())
                                {
                                    LoggerUtil.log.Information($"找到替代标签名称: {originalName} -> {altName}");
                                    alternativeItems.Add(new Item(new ItemIdentifier(altName)));
                                    found = true;
                                    break;
                                }
                            }
                            catch
                            {
                                // 忽略异常，继续尝试下一个
                            }
                        }
                        
                        if (!found)
                        {
                            LoggerUtil.log.Warning($"未找到标签 '{originalName}' 的有效替代格式");
                            alternativeItems.Add(originalItem); // 保留原始项
                        }
                    }
                    
                    // 如果找到了替代项，重新读取
                    if (alternativeItems.Count > 0 && alternativeItems.Any(i => i.ItemName.ToString() != filterItems.First().ItemName.ToString()))
                    {
                        LoggerUtil.log.Debug($"使用替代标签名称重新读取...");
                        values = server.Read(alternativeItems.ToArray());
                    }
                }
                
                if (values != null && values.Length > 0)
                {
                    LoggerUtil.log.Debug($"OPC DA 读取到 {values.Length} 个数据点");
                    
                    // 详细记录每个数据点的信息
                    for (int i = 0; i < values.Length; i++)
                    {
                        var value = values[i];
                        LoggerUtil.log.Debug($"数据点[{i}]: ItemName={value.ItemName}, Value={value.Value}, Quality={value.Quality}, Timestamp={value.Timestamp}, ResultID={value.ResultID}");
                        
                        // 检查数据质量
                        if (value.Quality != Opc.Da.Quality.Good)
                        {
                            LoggerUtil.log.Warning($"数据点[{i}] {value.ItemName} 质量不佳: {value.Quality}");
                        }
                        
                        // 检查结果ID
                        if (value.ResultID != null && value.ResultID.Failed())
                        {
                            LoggerUtil.log.Warning($"数据点[{i}] {value.ItemName} 读取失败: {value.ResultID}");
                            LoggerUtil.log.Warning($"建议：请使用OPC客户端工具验证标签名称是否正确。当前尝试的标签: {value.ItemName}");
                        }
                    }
                    
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
            ApplicationEvents.OnOpcConnectionChanged(false, "OPC连接断开");
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

        /// <summary>
        /// 递归浏览OPC服务器的所有标签
        /// </summary>
        /// <param name="parent">父节点标识</param>
        /// <param name="browseFilters">浏览过滤器</param>
        /// <returns>所有发现的标签列表</returns>
        private List<Item> BrowseAllItems(ItemIdentifier parent, BrowseFilters browseFilters)
        {
            var allItems = new List<Item>();
            var visitedBranches = new HashSet<string>();
            
            try
            {
                // 首先尝试只浏览分支
                LoggerUtil.log.Debug("尝试浏览分支...");
                BrowseRecursive(parent, browseFilters, allItems, visitedBranches, 0);
                
                // 如果分支浏览没有找到任何元素，尝试浏览所有元素
                if (allItems.Count == 0)
                {
                    LoggerUtil.log.Debug("分支浏览未找到元素，尝试浏览所有元素...");
                    var allFilters = new BrowseFilters
                    {
                        BrowseFilter = browseFilter.all,
                        ElementNameFilter = "",
                        MaxElementsReturned = 1000,
                        ReturnAllProperties = false,
                        ReturnPropertyValues = true,
                        VendorFilter = ""
                    };
                    visitedBranches.Clear();
                    BrowseRecursive(parent, allFilters, allItems, visitedBranches, 0);
                }
                
                // 如果仍然没有找到元素，尝试直接使用配置中的标签
                if (allItems.Count == 0)
                {
                    LoggerUtil.log.Debug("浏览未找到任何元素，尝试直接使用配置中的标签...");
                    var config = configurationService.GetConfiguration();
                    var regs = config.Registers;
                    
                    if (regs != null && regs.Count > 0)
                    {
                        foreach (var reg in regs)
                        {
                            try
                            {
                                var item = new Item(new ItemIdentifier(reg.Key));
                                allItems.Add(item);
                                LoggerUtil.log.Debug($"直接添加配置标签: {reg.Key}");
                            }
                            catch (Exception ex)
                            {
                                LoggerUtil.log.Warning(ex, $"添加配置标签失败: {reg.Key}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, "递归浏览OPC标签时发生异常");
            }
            
            return allItems;
        }

        /// <summary>
        /// 递归浏览OPC服务器的分支
        /// </summary>
        private void BrowseRecursive(ItemIdentifier parent, BrowseFilters browseFilters, List<Item> itemsList, HashSet<string> visitedBranches, int depth)
        {
            try
            {
                // 防止无限递归
                if (depth > 10)
                {
                    LoggerUtil.log.Warning($"浏览深度超过10层，停止递归: {parent.ItemName}");
                    return;
                }

                string parentPath = parent.ItemName ?? "";
                if (visitedBranches.Contains(parentPath))
                {
                    return; // 避免重复浏览
                }
                visitedBranches.Add(parentPath);

                BrowsePosition position;
                var browseElements = server.Browse(parent, browseFilters, out position);

                LoggerUtil.log.Debug($"浏览节点 '{parentPath}' 返回结果: browseElements={browseElements?.Length ?? 0}, position={position}");

                if (browseElements == null || browseElements.Length == 0)
                {
                    if (depth == 0)
                    {
                        LoggerUtil.log.Warning($"根节点未发现任何元素 - 浏览过滤器: BrowseFilter={browseFilters.BrowseFilter}, MaxElements={browseFilters.MaxElementsReturned}");
                    }
                    return;
                }

                LoggerUtil.log.Debug($"浏览节点 '{parentPath}' (深度 {depth})，发现 {browseElements.Length} 个元素");

                foreach (var element in browseElements)
                {
                    try
                    {
                        var elementName = element.Name;
                        var elementIdentifier = new ItemIdentifier(elementName);
                        
                        // 先尝试作为分支递归浏览
                        bool browsedAsBranch = false;
                        try
                        {
                            BrowsePosition testPosition;
                            var testBrowse = server.Browse(elementIdentifier, browseFilters, out testPosition);
                            if (testBrowse != null && testBrowse.Length > 0)
                            {
                                // 可以浏览，说明是分支
                                LoggerUtil.log.Debug($"发现分支: {elementName}，继续递归浏览...");
                                BrowseRecursive(elementIdentifier, browseFilters, itemsList, visitedBranches, depth + 1);
                                browsedAsBranch = true;
                            }
                        }
                        catch
                        {
                            // 浏览失败，可能是Item
                        }
                        
                        // 如果不能作为分支浏览，尝试作为Item
                        if (!browsedAsBranch)
                        {
                            try
                            {
                                var testItem = new Item(elementIdentifier);
                                var testResult = server.Read(new Item[] { testItem });
                                if (testResult != null && testResult.Length > 0)
                                {
                                    // 可以读取，说明是Item
                                    var item = new Item(elementIdentifier);
                                    itemsList.Add(item);
                                    LoggerUtil.log.Debug($"发现标签: {elementName}");
                                }
                            }
                            catch
                            {
                                // 既不是分支也不是Item，跳过
                                LoggerUtil.log.Debug($"跳过无法识别的元素: {elementName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerUtil.log.Warning(ex, $"处理浏览元素 '{element.Name}' 时发生异常");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.log.Error(ex, $"递归浏览节点 '{parent.ItemName}' 时发生异常");
            }
        }

        private void SetFilterItems()
        {
            var config = configurationService.GetConfiguration();
            var regs = config.Registers;
            filterItems = new List<Item>();
            
            LoggerUtil.log.Debug($"SetFilterItems 开始 - items数量: {items?.Count ?? 0}, regs数量: {regs?.Count ?? 0}");
            
            if (items != null && items.Count > 0)
            {
                LoggerUtil.log.Debug($"OPC服务器中的标签列表:");
                for (int i = 0; i < Math.Min(items.Count, 10); i++) // 只显示前10个
                {
                    LoggerUtil.log.Debug($"  [{i}] {items[i].ItemName}");
                }
                if (items.Count > 10)
                {
                    LoggerUtil.log.Debug($"  ... 还有 {items.Count - 10} 个标签");
                }
            }
            else
            {
                LoggerUtil.log.Warning("OPC服务器中未发现任何标签");
            }
            
            if (regs != null && regs.Count > 0)
            {
                LoggerUtil.log.Debug($"配置中注册的标签列表:");
                foreach (var reg in regs)
                {
                    LoggerUtil.log.Debug($"  {reg.Key} -> {reg.Value}");
                }
            }
            else
            {
                LoggerUtil.log.Warning("配置中未定义任何注册标签");
            }
            
            if (regs != null && regs.Count > 0 && items != null)
            {
                int matchedCount = 0;
                items.ForEach(item =>
                {
                    if (regs.ContainsKey(item.ItemName.ToString()))
                    {
                        filterItems.Add(item);
                        matchedCount++;
                        LoggerUtil.log.Debug($"匹配成功: {item.ItemName} -> {regs[item.ItemName.ToString()]}");
                    }
                });
                LoggerUtil.log.Information($"标签匹配完成 - 总标签: {items.Count}, 配置标签: {regs.Count}, 匹配成功: {matchedCount}");
            }
            else
            {
                LoggerUtil.log.Warning("无法进行标签匹配 - items或regs为空");
            }
        }

        #endregion
    }
}