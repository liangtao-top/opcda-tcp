# OPCDA2MSA
OPCDA 协议转 MAS 协议

## 配置 
```
{
  "autoStart": true,// 开机是否自动启动
  "opcda": {
    "host": "192.168.147.129",// 远程服务器地址
    "node": "Matrikon.OPC.Simulation.1",// OPC服务名称
    "username": "Administrator",// 用户名
    "password": "123456"// 密码
  },
  "msa": {
    "mn": 100000000,// 设备唯一编码
    "ip": "117.172.118.16",// MSA服务器地址
    "port": 31100,// MSA服务器端口
    "interval": 10000,// OPC数据上报至MSA服务器间隔周期，单位：毫秒
    "heartbeat": 5000// MSA远程远程连接心跳周期，单位：毫秒
  },
  "modbus": {
    "slave": {
      "ip": "0.0.0.0",// ModbusTCP 服务监听地址
      "port": 502,// ModbusTCP 服务监听端口
      "station": 1// 站号 slaveId
    }
  },
  "registers": { // 转发注册表，指标标签->指标编码
    "A.02GA_0729": "511110066004Q0003QT001",
    "A.02GA_0730": "511110066004Q0004QT001",
    "A.02PIA_0703B": "511110066004G0002YL001"
  },
  "logger": {
    "level": "debug",// 日记级别，Verbose，Debug，Info，Warn，Error，Fatal
    "file": "logs/log.txt" // 日志文件路径，删除配置项则不记录
  }
}
```

