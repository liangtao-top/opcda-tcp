using Opc;
using Opc.Da;
using OpcDAToMSA.modbus;
using OpcDAToMSA.utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace OPCDA2MSA.opc
{
    public class OpcNet
    {

        private readonly OpcCom.Factory fact = new OpcCom.Factory();

        private Opc.Da.Server server = null;

        public List<Item> items = null;

        public List<Item> filterItems = null;

        private readonly CfgJson cfg = Config.GetConfig();

        //定义枚举基于COM服务器的接口，用来搜索所有的此类服务器。
        private readonly IDiscovery discovery = new OpcCom.ServerEnumerator();

        //选择性的浏览地址空间。
        private readonly BrowseFilters filters = new BrowseFilters
        {
            ReturnAllProperties = true, //获取数据项的属性
            ReturnPropertyValues = true, //要求返回属性的值
        };

        //private readonly ModbusTcp modbusTcp = new ModbusTcp();

        private readonly MsaTcp msaTcp = new MsaTcp();

        private bool runing = true;

        private readonly CustomHttpClient customHttpClient = new CustomHttpClient();

        public OpcNet()
        {
            GetLocalServers();
            //modbusTcp.Run();
            msaTcp.Run();
        }

        // 获取计算机本地 Opc Server 列表
        public void GetLocalServers()
        {
            try
            {
                Opc.Server[] servers = discovery.GetAvailableServers(Specification.COM_DA_20);
                LoggerUtil.log.Debug("GetAvailableServers {@servers}, Length: {@Length}", servers, servers.Length);
                if (servers != null && servers.Length > 0)
                {
                    for (int i = 0; i < servers.Length; i++)
                    {
                        if (servers[i] != null)
                        {
                            LoggerUtil.log.Information("Opc.Server[{@i}] " + servers[i].Name, i);
                        }
                    }
                }
                else
                {
                    LoggerUtil.log.Information("无");
                }
            }
            catch (Exception e)
            {
                LoggerUtil.log.Warning(e, "GetAvailableServers");
            }
        }

        // 连接远程OPC服务器
        public void Connect()
        {
            this.runing = true;
            this.items = new List<Item>();
            this.filterItems = new List<Item>();

            string host = cfg.Opcda.Host;
            string node = cfg.Opcda.Node;

            _ = customHttpClient.PostAsync("http://localhost:31137/ui-events", new MemoryStream(Encoding.UTF8.GetBytes("{\"Event\":\"OpcDA\",\"Data\":\"连接\"}")));
            URL url = new URL($@"opcda://{host}/{node}");
            server = new Opc.Da.Server(fact, url);
            try
            {
                server.Connect(url, new ConnectData(new System.Net.NetworkCredential(cfg.Opcda.Username, cfg.Opcda.Password)));
                //server.Connect();
                LoggerUtil.log.Information($@"Opc Server {host} {node} is connected");
                _ = customHttpClient.PostAsync("http://localhost:31137/ui-events", new MemoryStream(Encoding.UTF8.GetBytes("{\"Event\":\"OpcDA\",\"Data\":\"运行\"}")));
                SetItems();
                string[] itemsNames = new string[items.Count];
                for (int i = 0; i < items.Count; i++)
                {
                    itemsNames[i] = items[i].ItemName;
                }
                LoggerUtil.log.Debug("Opc.Da.Server Read Items: {@itemsNames}, Length: {@Length}", itemsNames, itemsNames.Length);
                SetFilterItems();
                string[] filterItemsNames = new string[filterItems.Count];
                for (int i = 0; i < filterItems.Count; i++)
                {
                    filterItemsNames[i] = filterItems[i].ItemName;
                }
                LoggerUtil.log.Information("Opc.Da.Server Filter Items: {@filterItemsNames}, Length: {@Length}", filterItemsNames, filterItemsNames.Length);
            }
            catch (Exception e)
            {
                LoggerUtil.log.Fatal(e, "连接 Opc.Da.Server[" + host + "] 意外终止");
                if (runing)
                {
                    Thread.Sleep(cfg.Msa.Heartbeat);
                    Connect();
                }
            }
        }
        public void Stop()
        {
            this.runing = false;
            msaTcp.Stop();
            LoggerUtil.log.Information($@"Opc Server {cfg.Opcda.Host} {cfg.Opcda.Node} is stop");
            _ = customHttpClient.PostAsync("http://localhost:31137/ui-events", new MemoryStream(Encoding.UTF8.GetBytes("{\"Event\":\"OpcDA\",\"Data\":\"停止\"}")));
        }

        public void MsaTcp()
        {
            while (runing)
            {
                try
                {
                    if (filterItems.Count > 0)
                    {
                        ItemValueResult[] values = server.Read(filterItems.ToArray());
                        if (values != null && values.Length > 0)
                        {
                            msaTcp.Send(values);
                        }
                    }
                    else
                    {
                        LoggerUtil.log.Warning("Opc.Da.Server Read filterItems: {@filterItems}, Length: {@Length}", filterItems, filterItems.Count);
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtil.log.Fatal(ex, "Opc.Da.Server.Read 意外终止");
                    Connect();
                }
                Thread.Sleep(cfg.Msa.Interval);
            }
        }

        public void ModbusTcp()
        {
            //var regs = cfg.Registers;
            //while (true)
            //{
            //    //Console.Clear();
            //    try
            //    {
            //        //Console.ForegroundColor = ConsoleColor.Green;
            //        var values = server.Read(items.ToArray());
            //        if (values != null && values.Length > 0)
            //        {
            //            for (int i = 0; i < values.Length; i++)
            //            {
            //                if (values[i] != null)
            //                {
            //                    int index = regs[i];
            //                    System.Type type = values[i].Value.GetType();
            //                    //Console.WriteLine(type);
            //                    Console.WriteLine($@"{values[i].ItemName}@{index}={values[i].Value} {type}");

            //                    switch (values[i].Value.GetType().ToString())
            //                    {
            //                        case "System.Boolean":
            //                            Console.WriteLine($@"{BitConverter.ToString(ConvertUtil.BoolToBytes((bool)values[i].Value))}@{ConvertUtil.BoolToBytes((bool)values[i].Value).Length}");
            //                            modbusTcp.Store.HoldingRegisters[index] = BitConverter.ToUInt16(ConvertUtil.BoolToBytes((bool)values[i].Value),0);
            //                            break;
            //                        //case "System.Int16":
            //                        //    modbusTcp.Store.HoldingRegisters[index] = BitConverter.ToUInt16(BitConverter.GetBytes((short)values[i].Value), 0);
            //                        //    break;
            //                            //case "System.Int32":
            //                            //    modbusTcp.Store.HoldingRegisters[index] = BitConverter.ToUInt16(BitConverter.GetBytes((uint)values[i].Value), 0);
            //                            //    modbusTcp.Store.HoldingRegisters[index+1] = BitConverter.ToUInt16(BitConverter.GetBytes((uint)values[i].Value), 2);
            //                            //    break;
            //                    }

            //                    //byte[] bytes = ConvertUtil.getByte(values[i].Value);
            //                    //Console.WriteLine($@"{BitConverter.ToString(bytes)}@{bytes.Length}");

            //                    //if (bytes.Length > 2) { 
            //                    //    modbusTcp.Store.HoldingRegisters[index+1] = BitConverter.ToUInt16(bytes, 2);
            //                    //}
            //                    //BitConverter.ToUInt16((float)values[i].Value,0);
            //                    //modbusTcp.Store.HoldingRegisters[index] = BitConverter.ToUInt16(, 0);
            //                    //modbusTcp.Store.HoldingRegisters[index + 1] = BitConverter.ToUInt16(ConvertUtil.ObjectToBytes(values[i].Value), 2);
            //                }
            //            }
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        //Console.ForegroundColor = ConsoleColor.Red;
            //        Console.WriteLine(ex.Message);
            //        //throw;
            //    }
            //    Thread.Sleep(cfg.Opcda.Interval);
            //}
        }

        public void Subscription()
        {
            //设定组状态
            var state = new SubscriptionState();//组（订阅者）状态，相当于OPC规范中组的参数
            state.Name = "Group0";//组名
            state.ServerHandle = null;//服务器给该组分配的句柄。
            state.ClientHandle = Guid.NewGuid().ToString();//客户端给该组分配的句柄。
            state.Active = true;//激活该组。
            state.UpdateRate = 100;//刷新频率为1秒。
            state.Deadband = 0;// 死区值，设为0时，服务器端该组内任何数据变化都通知组。
            state.Locale = null;//不设置地区值。

            //添加组
            var subscription = (Subscription)server.CreateSubscription(state);//创建组

            //添加Item
            subscription.AddItems(items.ToArray());

            //注册回调事件
            subscription.DataChanged += new DataChangedEventHandler(OnDataChange);

            //以下测试同步读
            //以下读整个组
            //ItemValueResult[] values = subscription.Read(subscription.Items);

            //以下遍历读到的全部值
            //foreach (ItemValueResult value in values)
            //{
            //    Console.WriteLine("同步读：ItemName:{0}, Value:{1}, Quality:{2}, Timestamp:{3}", value.ItemName, value.Value, value.Quality, value.Timestamp);
            //}

            //以下测试异步读
            subscription.Read(subscription.Items, 1, OnReadComplete, out IRequest quest);
        }

        //DataChange回调
        public void OnDataChange(object subscriptionHandle, object requestHandle, ItemValueResult[] values)
        {
            foreach (ItemValueResult value in values)
            {
                Console.WriteLine("OnDataChange：ItemName:{0}, Value:{1}, Quality:{2}, Timestamp:{3}", value.ItemName, value.Value, value.Quality, value.Timestamp);
            }
            Console.WriteLine("事件信号句柄为：{0}", requestHandle);
        }

        //ReadComplete回调
        public void OnReadComplete(object requestHandle, ItemValueResult[] values)
        {
            foreach (ItemValueResult value in values)
            {
                Console.WriteLine("异步读：ItemName:{0}, Value:{1}, Quality:{2}, Timestamp:{3}", value.ItemName, value.Value, value.Quality, value.Timestamp);
            }
            Console.WriteLine("事件信号句柄为：{0}", requestHandle);
        }

        // 对全量Item过滤，只保留配置项中需要的Item
        private void SetFilterItems()
        {
            var regs = cfg.Registers;
            if (regs != null && regs.Count > 0)
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

        // 获取服务器上全部Item
        private void SetItems()
        {
            LoggerUtil.log.Debug("Server: {@server}", server);
            TreeNode node = new TreeNode(server.Name);
            LoggerUtil.log.Debug("RootNode: {@node}", node);
            BrowseAddress(node, null);//浏览根节点所包括的子项BrowseElement。过程Browse下文列出。
        }

        private void BrowseAddress(TreeNode node, BrowseElement parent)
        {//递归函数，浏览parent下所有的数据项，将这些项显示在控件TreeView的node节点下。
            if (parent != null && parent.IsItem == true)
                return;//如果BrowseElement对象是Item，则说明是组合的最后一级，终止递归。
            try
            {
                ItemIdentifier itemID = null;//BrowseElement和Item共同的父类。
                if (node.Tag != null && node.Tag.GetType() == typeof(BrowseElement))
                {//该节点是BrowseElement对象，而不是根节点。
                    parent = (BrowseElement)node.Tag;
                    //LoggerUtil.log.Debug("parent.ItemPath: {@Name}, parent.ItemName: {@ItemName}", parent.ItemPath, parent.ItemName);
                    //itemID = new ItemIdentifier(parent.ItemPath, parent.ItemName);

                    //LoggerUtil.log.Debug("parent.Name: {@Name},parent.ItemPath: {@ItemPath}, parent.ItemName: {@ItemName}", parent.Name, parent.ItemPath, parent.ItemName);
                    string TempName = parent.Name;
                    if (!string.IsNullOrEmpty(parent.ItemPath))
                    {
                        TempName = parent.ItemPath;
                    }
                    itemID = new ItemIdentifier(parent.ItemPath, TempName);
                }
                //if (parent != null)
                //{
                //    LoggerUtil.log.Debug("parent.Name: {@Name},parent.ItemPath: {@ItemPath}, parent.ItemName: {@ItemName}", parent.Name, parent.ItemPath, parent.ItemName);
                //    string TempName = parent.Name;
                //    if (!string.IsNullOrEmpty(parent.ItemPath)) {
                //        TempName = parent.ItemPath;
                //    }
                //    itemID = new ItemIdentifier(parent.ItemPath, TempName);
                //}

                BrowsePosition position = null;//地址空间巨大，则需要此使用此对象，一般不用。
                BrowseElement[] elements = server.Browse(itemID, filters, out position);
                if (elements != null)
                {//浏览到服务器m_server对应itemID所包含的元素。
                    foreach (BrowseElement element in elements)
                    {
                        //LoggerUtil.log.Debug("element.Name: {@Name},element.ItemName: {@ItemName},element.IsItem: {@IsItem},element.HasChildren: {@HasChildren}", element.Name, element.ItemName, element.IsItem, element.HasChildren);

                        if (!element.IsItem && element.HasChildren)
                        {
                            TreeNode newnode = AddBrowseElement(node, element);//加入到TreeView
                            //LoggerUtil.log.Debug("TreeNode: {@newnode}", newnode);
                            BrowseAddress(newnode, element);//递归调用
                        }
                        else if (element.IsItem == true)
                        {
                            //LoggerUtil.log.Debug("BrowseElement: {@element}", element);
                            items.Add(new Item
                            {
                                ClientHandle = Guid.NewGuid().ToString(),//客户端给该数据项分配的句柄。
                                ItemPath = element.ItemPath, //该数据项在服务器中的路径。
                                ItemName = element.ItemName //该数据项在服务器中的名字。
                            });
                        }


                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        //将浏览到的BrowseElement对象加入到控件TreeView中。
        private TreeNode AddBrowseElement(TreeNode previou, BrowseElement element)
        {
            TreeNode node = new TreeNode(element.Name);
            node.Tag = element;//将BrowseElement对象记录到节点。
            previou.Nodes.Add(node);//将节点加入到TreeView中。
            return node;// 返回node,由递归函数使用。
        }
    }


}
