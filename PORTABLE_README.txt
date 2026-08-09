Keyboard Screen Studio v1.0.2 绿色便携版

1. 双击 KeyboardScreenStudio.exe 启动，无需安装 .NET。
2. 首次启动会显示连接引导；程序会自动创建 Data 文件夹并保存本机配置。
3. 将获得合法使用权的 TTF 或 OTF 字体放入 Fonts 文件夹后，程序会自动识别。
4. 程序通过 HTTP POST 将 JPEG 直接推送到键盘设备。

隐私提醒

- Data\settings.json 包含设备地址、城市、股票代码和本地图片路径。
- AI 用量功能依赖用户自行安装的开源工具 Tokscale；KSS 仅在本机调用 Tokscale 的 JSON 命令，不读取 API Key，也不保存平台凭据。
- 设备传输为未加密 HTTP，只应在可信本地网络中使用。

第三方服务

- 天气数据由 Open-Meteo 提供，采用 CC BY 4.0：
  https://open-meteo.com/en/license
- 自动定位城市名由 BigDataCloud 解析。
- Yahoo Finance 股票数据与 Tokscale 本地用量均为非官方实验性集成，可能延迟、不准确或失效。
- 股票数据仅供信息展示，不构成投资建议。
- 本项目与 Linx68 品牌、设备制造商及上述平台不存在隶属、授权、认可或赞助关系。

许可证

项目源码采用 MIT License。应用图标与托盘图标采用单独的视觉资产许可。完整项目许可、隐私说明及第三方许可文件随程序提供。
