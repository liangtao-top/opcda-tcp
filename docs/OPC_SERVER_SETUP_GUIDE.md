# OPC服务器安装和配置指南

## 🚨 当前错误分析

根据日志显示的错误：
```
Opc.ConnectFailedException: E_NETWORK_ERROR
Could not connect to server. ---> System.Runtime.InteropServices.ExternalException: CoCreateInstanceEx: 服务器运行失败
```

这表明OPC服务器 `NT6000.eNetOPC.4` 未安装或未运行。

## 📋 解决方案

### 方案一：安装Matrikon OPC Simulation Server（推荐）

**Matrikon OPC Simulation Server** 是最常用的OPC测试服务器，免费且易于使用。

#### 1. 下载和安装
- 访问 [Matrikon官网](https://www.matrikonopc.com/products/opc-simulation-server.aspx)
- 下载 **Matrikon OPC Simulation Server**
- 安装到本地计算机

#### 2. 启动服务器
- 安装完成后，启动 **Matrikon OPC Simulation Server**
- 服务器名称通常是：`Matrikon.OPC.Simulation.1`

#### 3. 更新配置文件
修改 `config/config.json`：
```json
{
  "opcda": {
    "host": "localhost",
    "node": "Matrikon.OPC.Simulation.1",
    "type": "Everyone",
    "username": "",
    "password": ""
  }
}
```

### 方案二：使用其他OPC服务器

#### 常见的OPC服务器名称：
- `Matrikon.OPC.Simulation.1` - Matrikon仿真服务器
- `Kepware.KEPServerEX.V6` - Kepware服务器
- `OPC.SimaticNET` - Siemens服务器
- `RSLinx OPC Server` - Rockwell服务器

### 方案三：检查现有OPC服务器

#### 1. 使用OPC客户端工具
- 安装 **OPC Expert** 或 **OPC Client** 工具
- 扫描本地可用的OPC服务器
- 记录正确的服务器名称

#### 2. 使用Windows服务检查
```cmd
# 检查OPC相关服务
sc query | findstr OPC
```

#### 3. 使用注册表检查
```cmd
# 检查注册的OPC服务器
reg query "HKEY_CLASSES_ROOT\OPC" /s
```

## 🔧 代码改进

我已经在 `OpcNet.cs` 中添加了以下改进：

### 1. 服务器可用性检查
```csharp
private bool CheckLocalOpcServerAvailable(string serverName)
{
    // 检查本地OPC服务器是否可用
    var servers = fact.GetAvailableServers();
    // 显示所有可用服务器
    // 检查目标服务器是否存在
}
```

### 2. 详细的错误处理
- 区分本地和远程连接错误
- 提供具体的解决建议
- 显示可用服务器列表

### 3. 智能错误提示
- 根据错误类型提供针对性建议
- 推荐使用Matrikon仿真服务器进行测试

## 📊 测试步骤

### 1. 安装OPC服务器
```bash
# 下载并安装 Matrikon OPC Simulation Server
# 启动服务器服务
```

### 2. 更新配置
```json
{
  "opcda": {
    "host": "localhost",
    "node": "Matrikon.OPC.Simulation.1"
  }
}
```

### 3. 运行程序
```bash
# 重新启动应用程序
# 查看日志输出
```

### 4. 验证连接
- 检查日志中的服务器列表
- 确认连接成功
- 验证数据读取功能

## 🎯 预期结果

安装正确的OPC服务器后，您应该看到：

```
[Information] 检查本地OPC服务器: Matrikon.OPC.Simulation.1
[Information] 发现 1 个可用的OPC服务器:
[Information]   - Matrikon.OPC.Simulation.1
[Information] 找到目标服务器: Matrikon.OPC.Simulation.1
[Information] 正在连接OPC服务器: localhost/Matrikon.OPC.Simulation.1 (本地模式)
[Information] 使用本地连接，无需认证
[Information] 创建OPC服务器实例...
[Information] 正在连接OPC服务器...
[Information] OPC服务器连接成功，正在浏览项目...
[Information] Opc Server localhost Matrikon.OPC.Simulation.1 is connected (本地模式)
```

## 🆘 故障排除

### 常见问题：

1. **"未发现任何可用的OPC服务器"**
   - 解决：安装OPC服务器软件

2. **"未找到目标服务器"**
   - 解决：检查服务器名称是否正确

3. **"DCOM错误"**
   - 解决：检查DCOM配置和权限

4. **"网络错误"**
   - 解决：检查防火墙和网络设置

### 联系支持：
如果问题仍然存在，请提供：
- 完整的错误日志
- 操作系统版本
- 已安装的OPC服务器列表
- 网络配置信息
