# CryptoWatcher v3.0（WPF / .NET 8）

老版 WinForms 项目（v2.x）的现代化重构版。功能完全对齐，界面全部重做。

---

## 1. 本次重构做了什么

| 维度 | 旧版 v2.x | 新版 v3.0 |
|---|---|---|
| UI 框架 | WinForms | **WPF**（XAML 声明式界面） |
| 目标运行时 | .NET Framework 4.6.2 | **.NET 8（net8.0-windows）** |
| 窗口外观 | 系统默认边框 | 无边框 + 自定义标题栏 + 圆角卡片 + 阴影 |
| 第三方依赖 | Flurl + Newtonsoft.Json | **零依赖**（内置 `HttpClient` + `System.Text.Json`） |
| 列表渲染 | ListView 手工填充 | `ObservableCollection` + `DataTemplate` 数据绑定 |
| 图标 | 位图 | Segoe MDL2 Assets 矢量字形 |
| 崩溃诊断 | 无 | `%AppData%\CryptoWatcher\error.log` 落盘 |

**功能保持不变**：6 路交易所行情竞速、价格提醒（大于/小于/等于）、托盘常驻、桌面迷你窗、鼠标穿透、位置锁定、配置文件向后兼容。

---

## 2. 目录结构

```
CryptoWatcherWpf/
├─ NuGet.Config                 # 屏蔽本机全局 HTTP 源（否则 restore 报 NU1302）
├─ build-release.bat            # 一键构建脚本（双击即可）
├─ CryptoWatcherWpf.sln         # 解决方案（VS 2022 可直接打开）
└─ CryptoWatcher/
   ├─ CryptoWatcher.csproj      # .NET 8 WPF 工程，零 PackageReference
   ├─ GlobalUsings.cs           # 显式全局 using（刻意不含 System.Windows.Forms）
   ├─ App.xaml(.cs)             # 应用入口 + 全局异常兜底 + 崩溃日志
   ├─ Themes/Theme.xaml         # 浅色简约配色与控件样式
   ├─ Models/                   # Alert / CryptoItem（INotifyPropertyChanged）
   ├─ Services/
   │  ├─ AppPaths.cs            # %AppData%\CryptoWatcher\ 路径
   │  ├─ ConfigStore.cs         # config.json 读写（兼容旧版 Newtonsoft 格式）
   │  ├─ WebApis.cs             # 6 路竞速行情 + 市值 Top N
   │  ├─ PriceMonitor.cs        # 每项独立轮询循环 + 提醒判定 + 托盘通知
   │  ├─ CoinCatalog.cs         # 热门币种缓存
   │  └─ NativeMethods.cs       # 鼠标穿透 P/Invoke（64 位安全）
   ├─ MainWindow.xaml(.cs)      # 主界面
   ├─ MiniWindow.xaml(.cs)      # 桌面常驻迷你窗
   └─ Views/
      ├─ ItemEditWindow.xaml(.cs)   # 添加/编辑监测项
      └─ AlertEditWindow.xaml(.cs)  # 添加价格提醒
```

---

## 3. 构建

### 方式 A：双击 `build-release.bat`

默认产出**框架依赖单文件**：`publish\win-x64\CryptoWatcher.exe`（约 228 KB），
目标机器需安装 **.NET 8 Desktop Runtime**。

### 方式 B：命令行

```bat
:: 框架依赖单文件（体积小，需运行时）
dotnet publish CryptoWatcher\CryptoWatcher.csproj -c Release -r win-x64 ^
  --self-contained false -p:PublishSingleFile=true -o publish\win-x64

:: 自包含单文件（免安装运行时，约 150 MB）
build-release.bat portable
```

### 方式 C：Visual Studio 2022

打开 `CryptoWatcherWpf.sln` → 生成 → 发布。

> **构建前置**：需要 .NET 8 SDK 或更高版本（本机已有 9.0.317，可直接构建 net8.0 目标）。
> `NuGet.Config` 已把本机全局的 HTTP 源清空，只保留 nuget.org；本项目零第三方包，
> 该文件仅用于绕开 `NU1302`。

---

## 4. 运行与配置

- **配置文件**：`%AppData%\CryptoWatcher\config.json`（与旧版 v2.x **完全同路径同字段**，
  旧配置可直接被新版读取，无需迁移）。
- **币种缓存**：`%AppData%\CryptoWatcher\topcoins.json`
- **崩溃日志**：`%AppData%\CryptoWatcher\error.log`（仅出现未处理异常时才生成）

### 界面速览

- **主界面**：无边框窗口，标题栏可拖拽；右侧依次为 置顶 / 最小化到托盘 / 退出。
- **工具栏**：添加监测、编辑、删除、迷你模式。
- **列表**：关注项目 / 现价 / 涨跌 / 频率 / 状态；**涨红跌绿**（中国市场惯例）；双击行即编辑。
- **托盘右键**：显示主界面、迷你模式、锁定位置、鼠标穿透、退出。
- **迷你窗**：**隐蔽优先的窄条**（实测 140×148，6 个币种；单币种约 53px 高）。
  **只显示币种代码**（`AVAX`），不显示货币对（不显示 `/USDT`）—— 绑定的是
  `CryptoItem.Symbol`，主界面则仍用 `Key`（`AVAX/USDT`）不受影响。
  背景是 **60% 不透明度的白色**（`#99FFFFFF`），长时间置顶也能透出下方内容；
  已去掉投影、改用 1px 极淡描边维持轮廓（投影本身也会压暗下方内容）。
  币种名灰化弱化，视觉只突出价格（涨红跌绿）。
  可拖拽（顶部窄条）、可锁定位置、可鼠标穿透、可取消"总在最前"；
  **双击顶部窄条即恢复主界面**，右键菜单同样可恢复。
  想再调：改 `CryptoWatcher/MiniWindow.xaml` 的 `Width`、卡片 `Background` 前两位 alpha、
  以及行内的 `FontSize` / `Height`。
  > 宽度下限提示：内容区 = `Width - 28`。实测最长币种名 `MATIC`(29px) + 最长价格 `0.17430000`(66px)
  > 仍留 16px 间隙；`Width` 低于 135 时，长币种名会开始出现省略号。

---

## 5. 行情数据源

6 家交易所**同时竞速**，取最先返回有效价格的一家，并立即取消其余请求：

| 序号 | 交易所 | 接口 | 代码读取字段 |
|---|---|---|---|
| 1 | 火币 HTX | `api.huobi.pro/market/detail/merged` | `tick.close` |
| 2 | 币安 | `api.binance.com/api/v3/ticker/price` | `price`（字符串） |
| 3 | Bybit | `api.bybit.com/v5/market/tickers` | `result.list[0].lastPrice` |
| 4 | OKX | `www.okx.com/api/v5/market/ticker` | `data[0].last` |
| 5 | Gate.io | `api.gateio.ws/api/v4/spot/tickers` | `[0].last` |
| 6 | KuCoin | `api.kucoin.com/api/v1/market/orderbook/level1` | `data.price` |

均为公开接口，无需 API Key。

> **本机实测（2026-06-09）**：火币 / 币安 / Bybit / Gate.io / KuCoin **5 家正常返回**；
> **OKX 与 CoinCap 当前网络不通**。因是竞速机制，只要有一家通即可正常取价，不影响使用。
> CoinCap 仅用于「热门币种下拉列表」，不通时会退回内置的 20 个主流币种，同样不影响使用。

---

## 6. 已知事项 / 注意点

1. **框架依赖版需要 .NET 8 Desktop Runtime**。若目标机器没有，请用 `build-release.bat portable`
   构建自包含版本。
2. 迷你窗开启**鼠标穿透**后，窗口不再响应鼠标；请在**托盘右键菜单**里关闭该选项。
3. 刷新间隔下限 500 ms，界面可填 1–6000 秒。
4. 「等于」提醒使用万分之一容差比较，避免浮点精确比较永不触发。

---

## 7. 验证记录

- `dotnet build -c Release`：**0 警告 0 错误**
- `dotnet publish` 单文件产物：仅 `CryptoWatcher.exe` 一个文件
- 启动自检：进程正常存活、主窗口句柄有效、标题 `CryptoWatcher`、无崩溃日志
- 行情接口：5/6 家实测返回数据且字段与解析代码一致
