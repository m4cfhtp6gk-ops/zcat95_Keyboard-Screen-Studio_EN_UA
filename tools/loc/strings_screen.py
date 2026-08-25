# -*- coding: utf-8 -*-
# Text rendered onto the 142x428 keyboard screen. Space is extremely tight:
# a label column is ~49 px wide, the full safe width is 122 px. Keep the
# English and Ukrainian wording as short as the Chinese original.
SCREEN = [
    # ---- theme header titles -------------------------------------------------
    ("ScreenTitleSystem",      "PC status",  "Стан ПК",      "电脑状态"),
    ("ScreenTitleDashboard",   "Overview",   "Огляд",        "状态概览"),
    ("ScreenTitlePerformance", "Performance","Ресурси",      "性能条带"),
    ("ScreenTitlePerformanceVisual", "Graphs", "Графіки",  "性能可视化"),
    ("ScreenTitleNetwork",     "Network",    "Мережа",       "网络监控"),
    ("ScreenTitleNeonClock",   "Neon",       "Неон",         "霓虹时钟"),
    ("ScreenTitleFlipClock",   "Flip",       "Табло",        "翻页时钟"),
    ("ScreenTitleLocalTime",   "Local time", "Місцевий час", "本地时间"),
    ("ScreenTitleFiveDay",     "5-day",      "5 днів",       "五日天气"),
    ("ScreenTitleStocks",      "Stocks",     "Акції",        "股票"),
    ("ScreenTitleImage",       "Image clock","Час на фото",  "图片时间"),

    # ---- metric labels -------------------------------------------------------
    ("ScreenLabelCpu",           "CPU",       "CPU",        "CPU"),
    ("ScreenLabelMemory",        "Memory",    "Пам'ять",    "内存"),
    ("ScreenLabelMemoryShort",   "MEM",       "ПАМ",        "MEM"),
    ("ScreenLabelProcessor",     "Processor", "Процесор",   "处理器"),
    ("ScreenLabelDownload",      "Down",      "Завант.",    "下载"),
    ("ScreenLabelUpload",        "Up",        "Вивант.",    "上传"),
    ("ScreenLabelDownloadLong",  "Download",  "Завантаження","下载速度"),
    ("ScreenLabelUploadLong",    "Upload",    "Вивантаження","上传速度"),
    ("ScreenLabelUsage",         "usage",     "зайнято",    "占用率"),
    ("ScreenLabelCpuUsage",      "Processor", "Процесор",   "处理器占用"),
    ("ScreenLabelMemoryUsage",   "Memory",    "Пам'ять",    "内存占用"),
    ("ScreenLabelGpu",           "GPU",       "GPU",        "GPU"),
    ("ScreenGpuUnavailable",     "N/A",       "н/д",        "不可用"),

    # ---- clock labels --------------------------------------------------------
    ("ScreenLabelHours",       "Hours",   "Години",  "小时"),
    ("ScreenLabelMinutes",     "Minutes", "Хвилини", "分钟"),
    ("ScreenLabelSeconds",     "Seconds", "Секунди", "秒钟"),

    # ---- dot-matrix progress periods (right column is only ~39 px) -----------
    ("ScreenPeriodToday",   "Today", "День",   "今天"),
    ("ScreenPeriodWeek",    "Week",  "Тижд.",  "本周"),
    ("ScreenPeriodMonth",   "Month", "Місяць", "本月"),
    ("ScreenPeriodQuarter", "Qtr",   "Кв.",    "本季度"),
    ("ScreenPeriodYear",    "Year",  "Рік",    "本年"),

    # ---- weather -------------------------------------------------------------
    ("ScreenWeatherWaiting",  "No weather yet", "Немає погоди", "等待天气数据"),
    ("ScreenWeatherSetCity",  "Set a city in settings", "Вкажіть місто", "请在屏幕配置中填写城市"),
    ("ScreenWeatherStale",    "Last known data", "Останні дані", "上次天气数据"),
    ("ScreenWeatherUpdated",  "Updated {0}", "Оновлено {0}", "更新 {0}"),
    ("ScreenLabelFeelsLike",  "Feels", "Відчув.", "体感"),
    ("ScreenLabelHumidity",   "Humidity", "Вологість", "湿度"),
    ("ScreenToday",           "Today", "Сьогодні", "今天"),

    # ---- stocks --------------------------------------------------------------
    ("ScreenStocksEmpty",      "No quotes", "Немає даних", "暂无行情"),
    ("ScreenStocksAddSymbols", "Add symbols in settings", "Додайте коди", "请在屏幕配置中添加代码"),
    ("ScreenStocksStale",      "Last known data", "Останні дані", "上次行情数据"),

    # ---- music ---------------------------------------------------------------
    ("ScreenMusicPlaying", "Now playing", "Відтворення", "正在播放"),
    ("ScreenMusicPaused",  "Paused",      "Пауза",       "已暂停"),
    ("ScreenMusicStopped", "Not playing", "Не грає",     "未在播放"),
    ("MusicNothingPlaying","Nothing is playing", "Нічого не відтворюється", "没有正在播放的音乐"),
    ("MusicUnknownTrack",  "Unknown track", "Невідомий трек", "未知曲目"),

    # ---- image theme ---------------------------------------------------------
    ("ScreenImagePickPicture", "Choose a picture", "Оберіть фото", "请选择一张图片"),

    # ---- AI quota ------------------------------------------------------------
    ("ScreenAiWaitingSource", "Waiting for data", "Очікування даних", "等待数据源"),
    ("AiRemainingWithCount",  "{0} / {1}", "{0} / {1}", "{0} / {1}次"),
    ("AiResetHourly",         "Hourly",   "Щогодини",       "每小时"),
    ("AiResetDaily",          "Daily",    "Щодня",          "每日"),
    ("AiResetWeekly",         "Weekly",   "Щотижня",        "每周"),
    ("AiResetMonthly",        "Monthly",  "Щомісяця",       "每月"),
    ("AiResetBillingCycle",   "Billing cycle", "Платіжний цикл", "账期"),
    ("AiResetCustom",         "Custom",   "Власний",        "自定义"),

    # ---- weather conditions (Open-Meteo WMO codes) ---------------------------
    ("WeatherClear",          "Clear",            "Ясно",              "晴"),
    ("WeatherMostlyClear",    "Mostly clear",     "Майже ясно",        "大部晴朗"),
    ("WeatherPartlyCloudy",   "Partly cloudy",    "Хмарно",            "多云"),
    ("WeatherOvercast",       "Overcast",         "Похмуро",           "阴"),
    ("WeatherFog",            "Fog",              "Туман",             "雾"),
    ("WeatherDrizzle",        "Drizzle",          "Мряка",             "毛毛雨"),
    ("WeatherFreezingDrizzle","Icy drizzle",      "Крижана мряка",     "冻毛毛雨"),
    ("WeatherRain",           "Rain",             "Дощ",               "雨"),
    ("WeatherFreezingRain",   "Freezing rain",    "Крижаний дощ",      "冻雨"),
    ("WeatherSnow",           "Snow",             "Сніг",              "雪"),
    ("WeatherShowers",        "Showers",          "Злива",             "阵雨"),
    ("WeatherSnowShowers",    "Snow showers",     "Снігопад",          "阵雪"),
    ("WeatherThunderstorm",   "Thunderstorm",     "Гроза",             "雷雨"),
    ("WeatherUnknown",        "Weather",          "Погода",            "天气"),
    ("WeatherCurrentLocation","Current location", "Поточне місце",     "当前位置"),

    # ---- service and transport messages --------------------------------------
    ("DevicePushSucceeded",     "Push succeeded", "Надіслано", "推送成功"),
    ("DevicePushDeviceReturned","Device returned {0} {1}", "Пристрій відповів {0} {1}", "设备返回 {0} {1}"),
    ("DeviceConnectTimeout",    "Timed out connecting to the device", "Час очікування підключення вичерпано", "连接设备超时"),
    ("DeviceConnectFailed",     "Cannot reach the device: {0}", "Не вдалося підключитися до пристрою: {0}", "无法连接设备：{0}"),
    ("UpdateCheckTimeout",      "Timed out", "Час вичерпано", "超时"),
    ("WeatherCityNotFound",     "City not found: {0}", "Місто не знайдено: {0}", "没有找到城市：{0}"),
    ("WeatherNoCurrentData",    "The weather service returned no current conditions", "Служба погоди не повернула поточних даних", "天气接口没有返回当前天气"),
    ("StockUnsupportedSymbol",  "Symbol format not supported", "Формат коду не підтримується", "暂不支持当前代码格式"),
    ("StockParseFailed",        "Could not parse the quote data", "Не вдалося розібрати дані котирувань", "未能解析行情数据"),
    ("StockReadFailed",         "Cannot read {0}", "Не вдалося прочитати {0}", "无法读取 {0}"),
    ("StockSymbolNotFound",     "Symbol not found: {0}", "Код не знайдено: {0}", "没有找到代码：{0}"),
    ("StockMissingPreviousClose","{0} has no previous close", "{0}: немає ціни закриття", "{0} 缺少昨收数据"),
    ("StockQuoteMissingField",  "The quote is missing {0}", "У котируванні немає поля {0}", "行情缺少 {0}"),
    ("CommandTimedOut",         "The command timed out", "Час виконання команди вичерпано", "命令执行超时"),
    ("ErrorCannotResolveAppPath","Cannot determine the application path.", "Не вдалося визначити шлях до програми.", "无法确定程序路径。"),

    # ---- Tokscale ------------------------------------------------------------
    ("TokscaleNotDetected",     "Tokscale was not detected. Install it, then run tokscale once in a terminal.",
                                "Tokscale не виявлено. Встановіть його й один раз запустіть tokscale у терміналі.",
                                "未检测到 Tokscale。请先完成安装，并在终端运行一次 tokscale。"),
    ("TokscaleReadCount",       "Read {0} local usage entries.", "Прочитано {0} записів локального використання.", "已读取 {0} 项本地用量数据。"),
    ("TokscaleNoData",          "Tokscale runs, but has no data yet. Use a supported AI client, then check again.",
                                "Tokscale працює, але даних поки немає. Скористайтеся підтримуваним AI-клієнтом і повторіть перевірку.",
                                "Tokscale 可以运行，但暂时没有可用数据。请先使用受支持的 AI 客户端，再重新检测。"),
    ("TokscaleReadFailed",      "Tokscale could not be read. Check that tokscale runs in a terminal.",
                                "Не вдалося прочитати Tokscale. Перевірте, що tokscale запускається в терміналі.",
                                "Tokscale 读取失败，请在终端确认 tokscale 可以正常运行。"),
    ("TokscaleReadFailedDetail","Tokscale could not be read: {0}", "Не вдалося прочитати Tokscale: {0}", "Tokscale 读取失败：{0}"),

    # ---- Claude usage theme (142 px: labels and countdowns stay short) --------
    ("ScreenTitleClaude",       "Claude",        "Claude",        "Claude"),
    ("ScreenClaudeSession",     "Session",       "Сесія",         "会话"),
    ("ScreenClaudeWeek",        "Week",          "Тиждень",       "本周"),
    ("ScreenClaudeNotConnected","Not connected", "Не підключено", "未连接"),
    ("ScreenClaudeConnectHint", "Sign in to Claude Code", "Увійдіть у Claude Code", "请登录 Claude Code"),
    ("ScreenClaudeStale",       "Last known data", "Останні дані", "上次数据"),
    ("ScreenClaudeResetNow",    "resetting",     "скидання",      "重置中"),
    ("ScreenClaudeResetsAt", "resets at {0}", "скидання о {0}", "{0} 重置"),
    ("ScreenClaudeResetsOn", "resets {0} at {1}", "скидання {0} о {1}", "{0} {1} 重置"),
    ("ScreenClaudeDaysHours",   "{0}d {1}h",     "{0}д {1}год",   "{0}天{1}小时"),
    ("ScreenClaudeHoursMinutes","{0}h {1}m",     "{0}год {1}хв",  "{0}小时{1}分"),
    ("ScreenClaudeMinutes",     "{0}m",          "{0}хв",         "{0}分"),

    # ---- Claude usage service messages ---------------------------------------
    ("ClaudeRateLimited",
     "Anthropic is throttling requests - the screen will retry shortly",
     "Anthropic обмежує частоту запитів - екран спробує ще раз незабаром",
     "Anthropic 正在限制请求频率 - 屏幕稍后会重试"),
    ("ClaudeRequestFailed", "Anthropic returned {0}", "Anthropic повернув {0}", "Anthropic 返回 {0}"),
    ("BinanceRequestFailed", "Binance returned {0}", "Binance повернув {0}", "币安返回 {0}"),
    ("ClaudeNoWindows",      "The account reported no limit windows", "Акаунт не повернув жодного вікна лімітів", "账户未返回任何额度窗口"),
    ("ClaudeTokenRejected",
     "Anthropic rejected the Claude Code login ({0})",
     "Anthropic відхилив вхід Claude Code ({0})",
     "Anthropic 拒绝了 Claude Code 登录（{0}）"),
    ("ClaudeNoCredentials",
     "Claude Code is not signed in on this PC",
     "Claude Code не має входу на цьому ПК",
     "本机 Claude Code 尚未登录"),
    ("ClaudeCredentialExpired",
     "The Claude Code login has expired - run Claude Code and sign in",
     "Вхід Claude Code протух - запустіть Claude Code і увійдіть",
     "Claude Code 登录已过期 — 请运行 Claude Code 重新登录"),

    # ---- currency rates theme (142 px wide: keep short) -----------------------
    ("ScreenTitleCurrency",   "Rates",     "Курси",      "汇率"),
    ("ScreenCurrencyEmpty",   "No rates",  "Немає даних","暂无汇率"),
    ("ScreenCurrencyHint",    "Pick currencies in settings", "Оберіть валюти", "请在设置中选择货币"),
    ("CurrencyNoQuotes",      "No quote currencies are configured", "Не налаштовано жодної валюти", "未配置任何目标货币"),
    ("CurrencyRequestFailed", "ExchangeRate-API returned {0}", "ExchangeRate-API повернув {0}", "ExchangeRate-API 返回 {0}"),
    ("CurrencyUnknownCodes",  "None of the configured currency codes were recognized", "Жоден з указаних кодів валют не розпізнано", "配置的货币代码均无法识别"),

    # ---- crypto theme ---------------------------------------------------------
    ("ScreenTitleCrypto",     "Crypto",    "Крипто",     "加密货币"),

    # ---- pomodoro theme -------------------------------------------------------
    ("ScreenTitlePomodoro",     "Pomodoro",  "Помодоро",   "番茄钟"),
    ("ScreenPomodoroFocus",     "Focus",     "Фокус",      "专注"),
    ("ScreenPomodoroBreak",     "Break",     "Перерва",    "休息"),
    ("ScreenPomodoroIdle",      "Ready",     "Готово",     "就绪"),
    ("ScreenPomodoroStartHint", "Start from settings", "Старт у налаштуваннях", "请在设置或托盘中启动"),
    ("ScreenPomodoroCycles",    "{0}/{1} cycles", "{0}/{1} циклів", "{0}/{1} 循环"),
    # ---- hardware monitor theme (142 px: labels stay very short) --------------
    ("ScreenTitleHardware", "Hardware", "Апаратура", "硬件"),
    ("ScreenHwLoad",  "Load",  "Навант.", "负载"),
    ("ScreenHwTemp",  "Temp",  "Темп.",   "温度"),
    ("ScreenHwClock", "Clock, GHz", "Част., ГГц", "频率 GHz"),
    ("ScreenHwFan",   "Fan, rpm",   "Вент., rpm", "风扇 rpm"),
    ("ScreenHwVram",  "VRAM",  "VRAM",    "显存"),
    ("ScreenHwDisk",  "Disk",  "Диск",    "磁盘"),

    # ---- GitHub contribution theme --------------------------------------------
    ("ScreenTitleGitHub",       "GitHub", "GitHub", "GitHub"),
    ("ScreenGitHubNotConfigured","No account", "Немає акаунта", "未设置账号"),
        ("ScreenGitHubHint",
     "Set a username in settings",
     "Вкажіть ім'я користувача",
     "请在设置中填写用户名"),
    ("ScreenGitHubThisWeek",    "This week", "Цей тиждень", "本周"),
    ("ScreenGitHubWindow",      "{0} weeks", "{0} тижнів", "{0} 周"),
    ("GitHubUserNotFound",      "GitHub user not found: {0}", "Користувача GitHub не знайдено: {0}", "未找到 GitHub 用户：{0}"),
    ("GitHubRequestFailed",     "GitHub returned {0}", "GitHub повернув {0}", "GitHub 返回 {0}"),
    ("GitHubParseFailed",       "Could not read the contribution calendar", "Не вдалося прочитати календар контрибуцій", "无法读取贡献日历"),

    # ---- air alerts theme ------------------------------------------------------
    ("ScreenTitleAlerts",       "Alerts", "Тривоги", "警报"),
    ("ScreenAlertsActive",      "ALERT", "ТРИВОГА", "警报"),
    ("ScreenAlertsClear",       "Clear", "Чисто", "解除"),
    ("ScreenAlertsSince",       "since {0}", "з {0}", "{0} 起"),
    ("ScreenAlertsCount",       "Regions under alert: {0}", "Регіонів у тривозі: {0}", "警报地区：{0}"),
    ("ScreenAlertsRegions",     "regions under alert", "регіонів у тривозі", "个地区处于警报"),
    ("ScreenAlertsNotConfigured","No token", "Немає токена", "未设置令牌"),
    ("ScreenAlertsHint",        "Add an alerts.in.ua token in settings", "Додайте токен alerts.in.ua", "请在设置中填写 alerts.in.ua 令牌"),
    ("ScreenAlertsDisclaimer",  "Not your only alert source", "Не єдине джерело тривог", "仅供参考，非唯一警报来源"),
    ("ScreenAlertsLasted",      "Alert lasted", "Тривога тривала", "警报持续了"),
    ("ScreenAlertsMinutes",     "{0} min", "{0} хв", "{0} 分钟"),
    ("ScreenAlertsPopupAlert",  "ALERT", "ТРИВОГА", "防空警报"),
    ("ScreenAlertsPopupClear",  "ALL CLEAR", "ВІДБІЙ", "警报解除"),

    # ---- B6-B13 second-line themes ------------------------------------------------
    ("ScreenTitleCalendar",     "Calendar", "Календар", "日历"),
    ("ScreenCalendarUntitled",  "(untitled)", "(без назви)", "（无标题）"),
    ("ScreenCalendarEmpty",     "No calendar", "Немає календаря", "没有日历"),
    ("ScreenCalendarHint",      "Add an ICS link in the theme settings", "Додайте ICS-посилання в налаштуваннях теми", "请在主题设置中添加 ICS 链接"),
    ("ScreenCalendarNoEvents",  "Nothing upcoming", "Найближчих подій немає", "暂无近期日程"),
    ("ScreenCalendarAllDay",    "all day", "весь день", "全天"),
    ("ScreenCalendarTomorrow",  "Tomorrow", "Завтра", "明天"),
    ("ScreenTitleCountdown",    "Countdown", "Відлік", "倒计时"),
    ("ScreenCountdownEmpty",    "No events yet", "Ще немає подій", "尚无事件"),
    ("ScreenCountdownHint",     "Add a date in the theme settings", "Додайте дату в налаштуваннях теми", "请在主题设置中添加日期"),
    ("ScreenCountdownDays",     "days", "днів", "天"),
    ("ScreenCountdownHours",    "hours", "годин", "小时"),
    ("ScreenCountdownMinutes",  "minutes", "хвилин", "分钟"),
    ("ScreenCountdownArrived",  "It's here!", "Настало!", "已到来"),
    ("ScreenCountdownDaysShort","{0} d", "{0} дн", "{0} 天"),
    ("ScreenTitleWorldClock",   "World", "Світ", "世界"),
    ("ScreenWorldClockEmpty",   "No clocks yet", "Ще немає годинників", "尚无时钟"),
    ("ScreenWorldClockHint",    "Add cities in the theme settings", "Додайте міста в налаштуваннях теми", "请在主题设置中添加城市"),
    ("ScreenWorldClockBadZone", "Unknown time zone", "Невідомий часовий пояс", "未知时区"),
    ("ScreenWorldClockNextDay", "+1 day", "+1 день", "+1 天"),
    ("ScreenWorldClockPrevDay", "−1 day", "−1 день", "−1 天"),
    ("ScreenWorldClockSameTime","Same as local time", "Збігається з місцевим", "与本地时间相同"),
    ("ScreenWorldClockOffset",  "{0} h from local", "{0} год від вас", "与本地相差 {0} 小时"),
    ("ScreenTitlePing",         "Ping", "Пінг", "延迟"),
    ("ScreenPingEmpty",         "No hosts yet", "Ще немає хостів", "尚无主机"),
    ("ScreenPingHint",          "Add hosts in the theme settings", "Додайте хости в налаштуваннях теми", "请在主题设置中添加主机"),
    ("ScreenPingMs",            "{0} ms", "{0} мс", "{0} ms"),
    ("ScreenPingTimeout",       "Timeout", "Тайм-аут", "超时"),
    ("ScreenTitleDisks",        "Disks", "Диски", "磁盘"),
    ("ScreenDisksWaiting",      "No volumes found", "Томів не знайдено", "未找到磁盘"),
    ("ScreenDisksUsage",        "{0} of {1}", "{0} із {1}", "{0} / {1}"),
    ("ScreenAqi",               "AQI {0}", "AQI {0}", "AQI {0}"),
    ("ScreenAqiGood",           "good", "добре", "优"),
    ("ScreenAqiFair",           "fair", "прийнятно", "良"),
    ("ScreenAqiModerate",       "moderate", "помірно", "中"),
    ("ScreenAqiPoor",           "poor", "погано", "差"),
    ("ScreenAqiVeryPoor",       "very poor", "дуже погано", "很差"),
    ("ScreenAqiExtreme",        "hazardous", "небезпечно", "危险"),
    ("ScreenStocksPortfolio",   "PORTFOLIO", "ПОРТФЕЛЬ", "投资组合"),
    ("ScreenCurrencyDataDate",  "Data {0}", "Дані {0}", "数据 {0}"),
    # ---- synced from PR #22 (charts/hardware fixes made in another session) ------
    ("ScreenHwAdminLine1",      "Temps and fans need", "Темп. і вентилятори -", "温度与风扇需要"),
    ("ScreenHwAdminLine2",      "administrator rights", "потрібен адміністратор", "管理员权限"),
    ("ScreenHwNoSensors",       "No sensor readings here", "Сенсори не відповідають", "传感器无读数"),
    ("AirAlertBadToken",        "The alerts.in.ua token was rejected", "Токен alerts.in.ua відхилено", "alerts.in.ua 令牌被拒绝"),
    ("AirAlertRequestFailed",   "alerts.in.ua returned {0}", "alerts.in.ua повернув {0}", "alerts.in.ua 返回 {0}"),

    # ---- Telegram popup --------------------------------------------------------
    ("ScreenTgNewMessages", "{0} new message(s)", "{0} нових повідомлень", "{0} 条新消息"),


    # ---- language codes handed to third-party geo services -------------------
    # Open-Meteo geocoding accepts en/de/fr/es/it/pt/ru/tr/hi/zh, not uk.
    ("GeocodingLanguageCode",      "en", "en", "zh"),
    ("ReverseGeocodeLanguageCode", "en", "uk", "zh"),

    # ---- fonts ---------------------------------------------------------------
    ("FontDefaultMiSans", "Default (MiSans)", "Типовий (MiSans)", "默认 MiSans"),

    # ---- composer screen (v1.7.0) --------------------------------------------
    ("ComposerEmptyLine1", "Empty layout", "Порожній екран", "布局为空"),
    ("ComposerEmptyLine2", "Add widgets", "Додайте віджети", "请在主题设置中"),
    ("ComposerEmptyLine3", "in the theme settings", "в налаштуваннях теми", "添加小组件"),
    ("ScreenGitHubToday",  "Today", "Сьогодні", "今日"),
]
