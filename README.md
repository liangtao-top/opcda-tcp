# OPCDA2MSA
OPCDA 协议转 MAS 协议

## 安装运行

1. 下载安装 Windows 7 Ultimate，链接：ed2k://|file|cn_windows_7_ultimate_with_sp1_x64_dvd_u_677408.iso|3420557312|B58548681854236C7939003B583A8078|/  
2. Windows 7 设置用户名密码，关闭防火墙  
3. 在命令行运行control userpasswords2，打开win7系统的用户账户管理，找到“要使用本机，用户必须要输入账户和密码”，点击√，去掉默认勾选，点击应用按钮，输入密码确认
4. 对网卡插拔确认，并重命名标识lan1【Opc.Server】,lan2[MSA.Server]，并根据IP地址分布表设置网卡IP地址  
5. 下载 OpcDAToMSA安装包 内容到D盘根目录，链接：https://pan.baidu.com/s/1P5vZkUt8f3lS5d8RV0Nl1g 提取码：c2bd  
6. 按照顺序安装 windows6.1-kb4474419-v3-x64_b5614c6cea5cb4e198717789633dca16308ef79c.msu、ndp48-x86-x64-allos-enu.exe、OPC Core Components Redistributable (x86) 3.00.108.msi、npp.8.4.6.Installer.x64.exe  
7. 用Notepad++打开D:\Release\config.json配置文件，配置MN、OPC、MSA信息  
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
    "ip": "10.68.45.203",// MSA服务器地址
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
## Opc.Server服务端
###	本地安全策略
OPC 服务器和 OPCClient 服务器电脑都要设置：打开“控制面版”上的“管理工具”并打开
“本地安全策略”，点击安全选项—>”网络访问：本地帐户的共享和安全模式”->属性选择“经
典—本地用户以自己的身份验证”  
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