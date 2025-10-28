# OPC DA 连接配置指南

## 🔄 支持本地和远程两种连接模式

### 📋 配置参数说明

| 参数 | 说明 | 本地模式 | 远程模式 |
|------|------|----------|----------|
| `host` | OPC服务器地址 | `localhost` 或 `127.0.0.1` | 远程服务器IP或主机名 |
| `node` | OPC服务器节点名 | 本地服务器名称 | 远程服务器名称 |
| `type` | 认证类型 | `Everyone` | `Everyone` 或 `Windows` |
| `username` | 用户名 | 空字符串 `""` | 远程服务器用户名 |
| `password` | 密码 | 空字符串 `""` | 远程服务器密码 |

## 🏠 本地连接配置示例

### 示例1：Matrikon OPC 仿真服务器
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

### 示例2：使用计算机名
```json
{
  "opcda": {
    "host": ".",
    "node": "Matrikon.OPC.Simulation.1",
    "type": "Everyone",
    "username": "",
    "password": ""
  }
}
```

### 示例3：使用127.0.0.1
```json
{
  "opcda": {
    "host": "127.0.0.1",
    "node": "Matrikon.OPC.Simulation.1",
    "type": "Everyone",
    "username": "",
    "password": ""
  }
}
```

## 🌐 远程连接配置示例

### 示例1：远程OPC服务器
```json
{
  "opcda": {
    "host": "192.168.1.100",
    "node": "RemoteOPC.Server.1",
    "type": "Everyone",
    "username": "Administrator",
    "password": "password123"
  }
}
```

### 示例2：使用主机名
```json
{
  "opcda": {
    "host": "OPC-SERVER-01",
    "node": "OPC-SERVER-01.OPC.Server.1",
    "type": "Windows",
    "username": "domain\\user",
    "password": "password123"
  }
}
```

## 🔍 本地连接判断逻辑

系统会自动判断是否为本地连接，判断条件：

- `host` 为空或null
- `host` 为 `localhost`
- `host` 为 `127.0.0.1`
- `host` 为 `.`
- `host` 为 `本机`
- `host` 为 `local`
- `host` 等于当前计算机名

## ⚙️ 连接模式特点

### 🏠 本地模式特点
- ✅ **无需DCOM配置**：不需要复杂的DCOM设置
- ✅ **无需网络认证**：不需要用户名密码
- ✅ **性能更好**：本地通信延迟更低
- ✅ **配置简单**：只需确保OPC服务器运行
- ⚠️ **需要安装**：本地必须安装OPC服务器

### 🌐 远程模式特点
- ✅ **跨机器访问**：可以连接远程OPC服务器
- ✅ **集中管理**：OPC服务器集中部署
- ⚠️ **需要DCOM配置**：必须正确配置DCOM
- ⚠️ **需要网络认证**：需要用户名密码
- ⚠️ **网络依赖**：依赖网络连接稳定性

## 🛠️ 故障排除

### 本地连接问题
1. **OPC服务器未安装**
   - 安装Matrikon OPC Core Components
   - 或安装其他OPC服务器软件

2. **OPC服务器未运行**
   - 检查Windows服务中的OPC服务
   - 启动OPCEnum服务

3. **服务器名称错误**
   - 使用OPC Client工具测试连接
   - 确认正确的服务器名称

### 远程连接问题
1. **DCOM配置问题**
   - 参考docs目录中的DCOM配置截图
   - 设置正确的身份验证级别

2. **网络连接问题**
   - 测试ping连接
   - 检查防火墙设置

3. **权限问题**
   - 确保有足够的DCOM权限
   - 检查用户账户权限

## 📝 配置建议

- **开发测试**：使用本地模式，配置简单
- **生产环境**：根据实际需求选择本地或远程
- **混合环境**：可以动态切换配置
- **安全考虑**：远程连接注意密码安全
