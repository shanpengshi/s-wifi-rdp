# S-WiFi 远程桌面连接助手

<img src="assets/icon.png" width="96" height="96" alt="蓝色 S 与 Wi-Fi 图标" />

一个轻量的 Windows 桌面工具：查询本机 IPv4、分别管理控制端和被控端地址，并启动系统自带的远程桌面连接。适合网络允许互访的校园网、家庭或办公局域网。

**当前版本：v1.3.0 · 简体中文界面 · MIT 开源许可证**

## 下载和使用

从 [下载目录](downloads/) 获取 `S-WiFi-RDP-v1.3.0.zip`，解压到普通文件夹，双击 `RemoteDesktopHelper.exe`。校验值见 [SHA256SUMS.txt](downloads/SHA256SUMS.txt)。

本程序面向 Windows 10/11，使用 .NET Framework 4.8 和系统自带的 `mstsc.exe`。被控电脑需要支持接受远程桌面连接的 Windows 版本，例如专业版，并已开启远程桌面、设置账户密码和适当的访问权限。程序未进行代码签名。

1. 两台电脑分别打开程序，选择各自角色。
2. 点击“查询并自动填入”，核对本机网卡。
3. 控制端左侧填写被控电脑当前 IPv4。
4. 如需要按控制端 IP 放行，在被控电脑右侧填控制端 IP，复制放行命令，再在**被控电脑的管理员 PowerShell** 执行。
5. 在控制端点击“连接远程桌面”，在 Windows 窗口输入被控电脑的账户与密码。

详见 [完整使用说明](docs/使用说明.md)。

## 功能

- 控制端、被控端双角色和独立 IPv4 输入框。
- 本机 IPv4、网关、掩码、MAC 查询，多网卡选择。
- 启动时查询，网络地址变化时自动刷新本机输入框。
- 复制本机信息及电脑名、当前 Windows 用户名。
- 生成 TCP/UDP 3389 防火墙放行命令，仅复制，需手动执行。
- 调用原生 Windows 远程桌面，不接收或保存账户密码。
- 保存每台设备各自的角色、地址和网卡选择。
- 蓝色 S + Wi-Fi 图标，支持透明背景和多种 Windows 图标尺寸。

## 使用范围

自动查询只针对运行程序的本机；两台电脑之间没有自动同步、设备发现、云中转或穿透服务。程序不会设置固定 IP、自动开启远程桌面或修改路由。选择网卡只决定查询的信息，实际连接由 Windows 路由决定。

控制端 IP 变化后，若被控端规则仍限制旧 IP，需要在被控端重新执行更新后的命令。被控端 IP 变化后，需要更新控制端的目标地址。

公开版首次启动不预填任何个人 IP。文档及截图的 `192.0.2.*` 地址仅用于演示，不能直接用于实际连接。

## 构建与验证

在 Windows PowerShell 中运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

构建使用 Windows 的 .NET Framework C# 编译器，无需下载 NuGet 依赖。输出为 `dist/RemoteDesktopHelper.exe`。验证覆盖地址输入、启动参数、角色自动填入、离线地址清除和放行命令校验，不会发起远程连接或修改防火墙。

测试会读取本机可用网卡数量，但截图使用演示地址，且测试结果不纳入版本管理。

## 隐私与开源

本地设置文件 `last-ip.txt`、`helper-settings.ini` 已在 `.gitignore` 中排除。项目发布前移除了个人 IP、账户信息、本地路径、使用记录和旧构建产物；公开下载包从脱敏源码重新编译。

程序运行时会查询本机信息。“复制本机信息”会将地址、MAC、电脑名和当前用户名复制到本机剪贴板，由用户决定是否向他人分享。程序没有遥测或自动上传功能。系统远程桌面的凭据管理由 Windows 负责。

校园网是否扣费应以管理方现行规则及账单为准，本项目不作免流量承诺，也不代表任何学校或网络运营方。

项目采用 [MIT License](LICENSE)。许可证允许在保留版权和许可声明的条件下使用、修改与分发；它不是软件著作权登记证书、专利或代码签名证书。图标由 AI 辅助生成并进行透明通道和 ICO 封装处理，来源说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

窗口可以拖动边缘调整大小，也可以最大化。窄窗口会将两端地址上下排列；高度不足时滚动查看。请将 RemoteDesktopHelper.exe.config 与 exe 保持在同一目录，以启用高 DPI 配置。
