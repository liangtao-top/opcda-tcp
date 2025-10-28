# OPCDA2MSA
OPC DA 协议转 MSA 协议网关

## 🚀 新增功能特性

### 📊 系统监控与健康检查
- **实时监控**: 监控系统各组件运行状态
- **健康检查**: 定期检查 OPC DA、MSA、Modbus TCP、MQTT 连接状态
- **性能指标**: 收集 CPU、内存、线程数等系统指标
- **状态报告**: 生成详细的系统健康报告

### ⚙️ 配置管理优化
- **配置验证**: 自动验证配置文件完整性和有效性
- **默认配置**: 提供完整的默认配置模板
- **错误处理**: 完善的配置错误处理和恢复机制
- **配置备份**: 支持配置文件的备份和恢复

### 🔥 配置热更新
- **文件监控**: 实时监控配置文件变更
- **自动重载**: 配置文件变更时自动重新加载
- **服务重启**: 配置变更时自动重启相关服务
- **事件通知**: 配置变更事件通知机制

### 🏗️ 架构重构
- **分层架构**: UI层、服务层、配置层、监控层分离
- **服务管理**: 统一的服务生命周期管理
- **协议适配**: 支持多种工业协议（MSA、MQTT、Modbus TCP）
- **异步处理**: 全异步的服务操作

### 🔧 开发测试秘钥
123456

## 📁 项目结构

```
OpcDAToMSA/
├── src/                    # 源代码目录
│   ├── UI/                 # 用户界面
│   │   ├── Forms/          # 窗体
│   │   └── Controls/       # 自定义控件
│   ├── Core/               # 核心业务逻辑
│   ├── Protocols/          # 协议适配器
│   ├── Services/           # 服务层
│   ├── Configuration/      # 配置管理
│   ├── Monitoring/         # 监控服务
│   └── Utils/              # 工具类
├── config/                 # 配置文件目录
├── docs/                   # 文档
└── tests/                  # 测试目录
```

## 🔧 配置说明

### 新版配置文件结构
```json
{
  "autoStart": false,
  "opcda": {
    "host": "192.168.147.129",
    "node": "Matrikon.OPC.Simulation.1",
    "type": "Everyone",
    "username": "Administrator",
    "password": "123456"
  },
  "protocols": {
    "msa": {
      "enabled": true,
      "settings": {
        "mn": 100000000,
        "ip": "117.172.118.16",
        "port": 31100,
        "interval": 10000,
        "heartbeat": 5000
      }
    },
    "mqtt": {
      "enabled": true,
      "settings": {
        "broker": "mqtt://broker.example.com:1883",
        "clientId": "opcda-gateway",
        "username": "mqtt_user",
        "password": "mqtt_pass",
        "topics": {
          "data": "opcda/data",
          "status": "opcda/status"
        },
        "qos": 1,
        "retain": false,
        "cleanSession": true
      }
    },
    "modbusTcp": {
      "enabled": true,
      "settings": {
        "ip": "0.0.0.0",
        "port": 502,
        "station": 1,
        "registerMapping": {
          "OPC的模拟量serveNAOH.02LIA_0701A": 1000,
          "OPC的模拟量serverHCN.LIC033201": 1001,
          "OPC的模拟量serverHCN.LI03102": 1002
        }
      }
    }
  },
  "registers": {
    "OPC的模拟量serveNAOH.02LIA_0701A": "511110066004Q0003QT001",
    "OPC的模拟量serverHCN.LIC033201": "511110066004Q0004QT001",
    "OPC的模拟量serverHCN.LI03102": "511110066004G0002YL001"
  },
  "logger": {
    "level": "debug",
    "file": "logs/log.txt"
  }
}
```

### 配置项说明
- **autoStart**: 开机是否自动启动
- **opcda**: OPC DA 服务器配置
- **protocols**: 协议配置（支持 MSA、MQTT、Modbus TCP）
- **registers**: 数据点映射表（OPC标签 -> 编码）
- **logger**: 日志配置

## 🚀 快速开始

### 1. 环境要求
- Windows 7/10/11
- .NET Framework 4.8
- OPC Core Components

### 2. 安装步骤
1. 下载并安装 .NET Framework 4.8
2. 安装 OPC Core Components
3. 下载 OpcDAToMSA 安装包
4. 解压到目标目录
5. 配置 `config/config.json` 文件

### 3. 启动应用
```bash
# 直接运行
OpcDAToMSA.exe

# 或使用服务管理器启动
ServiceManager.Instance.StartAllServicesAsync()
```

## 📊 监控功能

### 健康检查
```csharp
// 启动监控服务
MonitoringService.Instance.Start();

// 获取健康报告
var report = MonitoringService.Instance.GetHealthReport();
Console.WriteLine($"系统状态: {report.OverallStatus}");
```

### 指标收集
```csharp
// 更新自定义指标
MonitoringService.Instance.UpdateMetric("custom_metric", 100.5, "count");

// 获取所有指标
var metrics = MonitoringService.Instance.Metrics;
```

## ⚙️ 配置管理

### 配置热更新
```csharp
// 启用配置热更新
ConfigurationManager.Instance.EnableHotReload();

// 监听配置变更
ConfigurationManager.Instance.ConfigurationChanged += OnConfigChanged;

// 手动重新加载配置
ConfigurationManager.Instance.ReloadConfiguration();
```

### 配置验证
```csharp
// 验证配置
var config = ConfigurationManager.Instance.CurrentConfig;
var validation = ValidateConfiguration(config);

if (!validation.IsValid)
{
    Console.WriteLine($"配置错误: {validation.ErrorMessage}");
}
```

## 🔌 协议支持

### MSA 协议
- 支持 MSA 3.0 协议
- 自动心跳保持连接
- 数据点映射和转换

### MQTT 协议
- 支持 MQTT 3.1.1
- 可配置 QoS 级别
- 支持主题订阅和发布

### Modbus TCP 协议
- 支持 Modbus TCP 从站模式
- 可配置寄存器映射
- 支持多种数据类型转换

## 🛠️ 开发指南

### 添加新协议
1. 实现 `IProtocolAdapter` 接口
2. 在 `ProtocolRouter` 中注册
3. 更新配置文件结构

### 自定义监控指标
```csharp
// 注册健康检查
MonitoringService.Instance.RegisterHealthCheck("custom_component", () => 
{
    return new HealthStatus
    {
        ComponentName = "custom_component",
        Status = HealthStatusType.Healthy,
        Timestamp = DateTime.Now
    };
});
```

## 📝 更新日志

### v2.0.0 (2024-10-28)
- ✨ 新增系统监控和健康检查功能
- ✨ 新增配置热更新功能
- ✨ 新增 MQTT 协议支持
- 🏗️ 重构项目架构，采用分层设计
- 🔧 优化配置管理，支持配置验证
- 📊 新增性能指标收集
- 🛡️ 增强错误处理和异常恢复

### v1.0.0
- 🎉 初始版本发布
- ✅ 支持 OPC DA 到 MSA 协议转换
- ✅ 支持 Modbus TCP 协议
- ✅ 基础配置管理

## 🤝 贡献指南

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开 Pull Request

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情

## 📞 联系方式

- 项目链接: [https://github.com/liangtao-top/opcda-tcp](https://github.com/liangtao-top/opcda-tcp)
- 问题反馈: [Issues](https://github.com/liangtao-top/opcda-tcp/issues)

---

## 安装运行

1. 下载安装 Windows 7 Ultimate，链接：ed2k://|file|cn_windows_7_ultimate_with_sp1_x64_dvd_u_677408.iso|3420557312|B58548681854236C7939003B583A8078|/  
2. Windows 7 设置用户名密码，关闭防火墙  
3. 在命令行运行control userpasswords2，打开win7系统的用户账户管理，找到"要使用本机，用户必须要输入账户和密码"，点击√，去掉默认勾选，点击应用按钮，输入密码确认
4. 对网卡插拔确认，并重命名标识lan1【Opc.Server】,lan2[MSA.Server]，并根据IP地址分布表设置网卡IP地址  
5. 下载 OpcDAToMSA安装包 内容到D盘根目录，链接：https://pan.baidu.com/s/1P5vZkUt8f3lS5d8RV0Nl1g 提取码：c2bd  
6. 按照顺序安装 windows6.1-kb4474419-v3-x64_b5614c6cea5cb4e198717789633dca16308ef79c.msu、ndp48-x86-x64-allos-enu.exe、OPC Core Components Redistributable (x86) 3.00.108.msi、npp.8.4.6.Installer.x64.exe  
7. 用Notepad++打开D:\Release\config\config.json配置文件，配置MN、OPC、MSA信息  

## Opc.Server服务端
## Opc.Server服务端
###	本地安全策略
在运行中输入：secpol.msc，打开“本地安全策略”，点击安全选项—>”网络访问：本地帐户的共享和安全模式”->属性选择“经典—本地用户以自己的身份验证”  
 ![图片名称](/docs/dcom-9.png) 
###	DCOM 的配置
1.	在命令行运行 dcomcnfg.  
![图片名称](/docs/dcom-0.png)![图片名称](/docs/dcom-1.png)
2.	在上面的[默认属性]页面中， 将“在这台计算机上启用分布式COM”打上勾，将<默认身份验证级别>设置为<无>，将<默认模拟级别>设置为<标识>。  
![图片名称](/docs/dcom-2.png)  
3.	在[COM 安全]属性页中，将和都增加分别添加everyone， administrator， anonymous logon 用户及建立的相同用户，并选中其所有权限。  
![图片名称](/docs/dcom-3.png) 
###	配置 Opcenum 属性
1.	点开左侧树形列表[组件服务->计算机->我的电脑->DCOM 配置]  
![图片名称](/docs/dcom-4.png)  
2.	在左侧的 DCOM 程序中找到 OpcEnum  
![图片名称](/docs/dcom-5.png)  
3.	右键点击<OpcEnum>，弹出的右键菜单，点击<属性>，弹出对话框设置身份验证级别。将<身份验证级别>设置为<无>  
![图片名称](/docs/dcom-6.png) 
4.	配置安全。全部选择<自定义>  
 ![图片名称](/docs/dcom-7.png) 
5.	并将<启动和激活权限>、 <访问权限>、 <配置权限>都增加everyone，administrator， anonymous logon 用户及建立的相同用户，并配置全部权限  
 ![图片名称](/docs/dcom-8.png) 
###	配置对应的 OPC Server 
对应的 OPC Server 设置（可不做设置）配置方法与 OpcEnum 一样，首先要了解所用的 OPC 对应的组件。如MatrikonOPCSimulation.exe