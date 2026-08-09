<p align="center"><img src="docs/images/keyboard-screen-studio-hero.png" alt="Keyboard Screen Studio for Linx68" /></p>

# Keyboard Screen Studio

Keyboard Screen Studio（KSS）是一款面向 Linx68 键盘竖屏的桌面工具，可把系统状态、时钟、天气、行情、媒体信息和图片主题生成至 142×428 屏幕，并通过键盘的图像 API 自动推送。

## 当前状态

- **Windows：** v1.1.0，已迁移到 Avalonia UI，作为当前正式发布平台。
- **macOS：** 已保留跨平台项目和发布脚本，Intel 与 Apple Silicon 包仍待真实设备测试，暂不作为稳定版本发布。
- **Linux：** 暂未发布；核心数据、渲染与平台接口已为后续适配留出空间。

## 主要功能

- 19 个内置主题，涵盖监控、时间、资讯、音乐、点阵与图片时间。
- 142×428 实时预览，保留键盘固件状态栏安全区。
- 自定义主题强调色、屏幕字体、内容安全区和图片时间排版。
- 亮色、暗色和跟随系统的应用界面，不影响键盘主题输出。
- 定时刷新与推送、媒体主题自动切换、托盘驻留和开机启动。
- 首次启动向导仅需填写键盘 IP 地址。
- AI 用量（开发中）可读取用户自行安装配置的 Tokscale 数据；KSS 不保存平台凭据。

## 使用要求

- Windows 10 19041 或更高版本。
- 电脑与键盘处于同一局域网。
- 在键盘菜单中启用“图像 API”，然后将显示的 IP 地址填入 KSS。

下载 Release 中的 `KeyboardScreenStudio-v1.1.0-win-x64.zip`，解压后运行 `KeyboardScreenStudio.exe`。发布包为自包含版本，无需另装 .NET。

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
