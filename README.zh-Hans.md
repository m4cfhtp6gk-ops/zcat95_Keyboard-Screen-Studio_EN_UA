<p align="center"><img src="docs/images/keyboard-screen-studio-hero.png" alt="Keyboard Screen Studio for Linx68" /></p>

# Keyboard Screen Studio

*阅读 [English](README.md) · [Українською](README.uk.md)*

Keyboard Screen Studio（KSS）是一款面向 Linx68 键盘竖屏的桌面工具，可把系统状态、时钟、天气、行情、媒体信息和图片主题生成至 142×428 屏幕，并通过键盘的图像 API 自动推送。

## 当前状态

- **Windows：** v1.10.4，已迁移到 Avalonia UI，作为当前正式发布平台。
- **macOS：** 已保留跨平台项目和发布脚本，Intel 与 Apple Silicon 包仍待真实设备测试，暂不作为稳定版本发布。
- **Linux：** 暂未发布；核心数据、渲染与平台接口已为后续适配留出空间。

## 主要功能

- 33 个内置主题，涵盖监控、时间、资讯、音乐、点阵与图片时间——包括可自由拼装小组件的"自定义屏幕"构建器、分页轮播的硬件监控、汇率、带走势图的币安加密货币、番茄钟、GitHub 贡献网格、磁盘占用、网络延迟、世界时钟、事件倒计时、ICS 日历订阅，以及基于 alerts.in.ua 数据的乌克兰防空警报（仅供参考，切勿作为唯一警报来源），并可选警报优先：全屏或弹窗横幅，持续到解除或按设定时长。另可用 Linx68 音量旋钮直接切换主题（右旋/左旋/按下暂停轮播，可按 VID:PID 绑定本键盘并拦截音量键，或改用自己录制的组合键完全不碰音量，默认关闭）。另有主题轮播、每主题独立刷新间隔与强调色、12/24 小时制与 °C/°F 单位、天气页的日出日落与空气质量、按持仓数量的股票组合行、镜像每一帧的额外键盘，以及去除机密信息的设置导出/导入。
- 夜间计划（在设定时段切换主题并降低亮度）、来自您账号的 Telegram 弹窗（覆盖任意主题），以及可选的 Windows 通知（Claude 额度、键盘离线、价格提醒），均带防刷屏冷却。
- 142×428 实时预览，保留键盘固件状态栏安全区。
- 自定义主题强调色、屏幕字体、内容安全区和图片时间排版。
- 亮色、暗色和跟随系统的应用界面，不影响键盘主题输出。
- 界面与主题支持英文、乌克兰文和简体中文，可在“其他设置”中随时切换。
- 定时刷新与推送、媒体主题自动切换、托盘驻留和开机启动。
- 首次启动向导仅需填写键盘 IP 地址。
- AI 用量（开发中）可读取用户自行安装配置的 Tokscale 数据；KSS 不保存平台凭据。

## 使用要求

- Windows 10 19041 或更高版本。
- 电脑与键盘处于同一局域网。
- 在键盘菜单中启用“图像 API”，然后将显示的 IP 地址填入 KSS。

下载 Release 中的 `KeyboardScreenStudio-v1.10.4-win-x64.zip`，解压后运行 `KeyboardScreenStudio.exe`。发布包为自包含版本，无需另装 .NET。

## 从源码构建

```powershell
dotnet restore KeyboardScreenStudio.sln
dotnet build src/KeyboardScreen.App.Avalonia/KeyboardScreen.App.Avalonia.csproj -c Release
dotnet run --project tests/KeyboardScreen.SmokeTests/KeyboardScreen.SmokeTests.csproj -c Release
```

创建 Windows 绿色包：

```powershell
./tools/Publish-Portable.ps1
```

> 本地建议安装 .NET 9 SDK 以获得完整的 Avalonia 编译期诊断；使用 .NET 8 SDK 亦可构建（会有一条分析器版本警告）。

## 数据与隐私

KSS 的设置和登录状态只保存在本机。天气、行情和 AI 用量依赖第三方数据源，可能出现延迟、不准确或接口变化；股票信息仅用于桌面展示，不构成投资建议。详见 [PRIVACY.md](PRIVACY.md) 和 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## 开源协议

源代码采用 [MIT License](LICENSE)。应用图标和品牌资产说明见 [ASSET_LICENSE.md](ASSET_LICENSE.md)。
