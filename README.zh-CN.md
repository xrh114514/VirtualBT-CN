# VirtualBT

**Windows 虚拟蓝牙输入设备** —— 将计算机模拟为蓝牙键盘或鼠标，并将键鼠操作转发至手机、平板等设备。

界面支持中文与 English 切换。

> **English**：[README.md](README.md)
>
> **使用说明**：见 [快速上手.md](快速上手.md)；详细排错见下文[疑难排错](#疑难排错)。
>
> **原项目**：[itsmikethetech/VirtualBT](https://github.com/itsmikethetech/VirtualBT) · [VirtualDrivers/VirtualBT](https://github.com/VirtualDrivers/VirtualBT)

## 目录

- [功能](#功能)
- [快速开始](#快速开始)
- [系统要求](#系统要求)
- [疑难排错](#疑难排错)
- [目录结构](#目录结构)
- [从源码构建](#从源码构建)
- [已知限制](#已知限制)
- [开发文档](#开发文档)
- [参与改进](#参与改进)
- [致谢](#致谢)
- [许可](#许可)

---

## 功能

| 功能 | 说明 |
|---|---|
| 蓝牙键盘模拟 | 将计算机按键实时转发至已连接设备（BLE HID） |
| 蓝牙鼠标模拟 | 支持相对位移、左/右/中键与滚轮（BLE HID，16 位精度） |
| 全屏游戏模式 | 独占全屏并隐藏指针，同时捕获鼠标与键盘，适用于视角控制 |
| BLE 设备扫描与配对 | 扫描周边低功耗蓝牙设备，并在应用内完成配对 |
| 中英双语 | 设置页可切换，重启应用后生效 |
| GATT 服务调试 | 查看、读写特征值，广播信标，广播监听等（源自 BluetoothLEExplorer） |

---

## 快速开始

### 1. 启动

双击运行 **`启动VirtualBT.bat`**。

脚本将自动执行以下操作：

- 检查并安装缺失的 .NET Native 1.6 运行时；
- 注册应用包；
- 启动程序；若程序已在运行，则将其窗口切换至前台。

成功时窗口自动关闭；出错时窗口保留并显示错误信息。

### 2. 将计算机用作蓝牙键盘或鼠标

1. 确认系统蓝牙已开启（任务栏快捷设置）。
2. 打开 VirtualBT，依次进入左侧菜单中的 **虚拟蓝牙键盘**。
3. 开启 **“开启广播：可被发现且可连接”** 开关。
4. 在手机的蓝牙设置中搜索并配对计算机。
5. 连接成功后，“已连接的客户端列表”中将显示该设备。

此后，当计算机窗口获得焦点时，键盘输入将转发至手机。

### 3. 游戏模式（鼠标与键盘全屏捕获）

1. 确认手机已连接（客户端列表中存在设备）。
2. 点击 **“进入鼠标捕获模式（全屏）”**。
3. 进入黑底全屏界面后，鼠标移动用于视角控制，键盘输入用于移动或操作。

| 按键 | 作用 |
|---|---|
| **`Esc`** | 退出全屏（不转发至手机） |
| **`F1`** | 显示或隐藏提示条 |
| **`小键盘 +` / `-`** | 调整灵敏度，范围 0.1x 至 5.0x |
| 其他任意键 | 全部转发至手机（包括 `Tab`、`WASD`、`Shift` 等） |

### 4. 切换语言

**设置 → 语言 → 中文 / English**。应用将自动重启，重启后生效。

---

## 系统要求

- Windows 10 版本 1903+（内部版本 18362+），已验证兼容 Windows 11。
- 支持 **BLE 外设（Peripheral）角色**的蓝牙适配器。
  - 常见 Intel 无线蓝牙（AX 系列）需使用较新驱动。
  - 如不确定，建议先测试；若出现 `RadioNotAvailable`，参见下方排错说明。
- 开发者模式或允许侧载应用（安装应用包时需要）。

---

## 疑难排错

### `无法创建HID服务提供程序：RadioNotAvailable`

蓝牙广播资源不可用，键盘与鼠标均无法发送。应用会自动重试 6 次，并给出诊断提示。请按顺序尝试：

1. 确认系统蓝牙已开启（最常见原因）。
2. 关闭手机连接、Phone Link 等占用蓝牙的程序。
3. 关闭蓝牙，等待 5 秒后重新开启。
4. 在设备管理器中定位 蓝牙 → `英特尔(R) 无线 Bluetooth(R)`，右键选择“禁用”，再选择“启用”。
5. 更新 Intel 无线蓝牙驱动。
6. 重启计算机。

若上述方法均无效，说明该网卡不支持外设角色。请更换支持 BLE Peripheral 的 USB 蓝牙适配器（如 CSR8510 或较新的 Realtek BLE 5.x）。

### 手机无法搜索到计算机

- 计算机端开关未开启（“开启广播：可被发现且可连接”）。
- 在手机蓝牙设置中手动执行“搜索设备”，不应仅依赖快捷面板。
- 部分机型需关闭并重新开启手机蓝牙，方可发现 HID 设备。

### “扫描与配对设备”页点击“配对”提示没有权限

此为正常现象。手机通常不作为蓝牙外设被计算机连接。若需将计算机作为键盘供手机使用，请使用“虚拟蓝牙键盘”页，并在手机端执行配对，连接方向相反。

### 鼠标移动至屏幕边缘后停止

UWP 没有鼠标锁定 API。当指针到达屏幕边界时，不再上报相对位移。单屏全屏使用通常可满足需求。

### 切换语言后未生效

设置页切换会保存选择并自动重启。若仍未生效，请检查 Windows 设置中应用的语言权限。

---

## 目录结构

```
VirtualBT-main/
├── 启动VirtualBT.bat          # 启动入口（推荐）
├── launch-virtualbt.ps1       # 启动逻辑（依赖安装、注册、启动、置前）
├── deploy/
│   ├── AppX/                  # 已部署的应用包（构建产物）
│   ├── nuget/                 # .NET Native 1.6 运行时 appx
│   └── refasm/                # 编译所需的 .NETCore v5.0 引用程序集
├── BluetoothLEExplorer/       # 主工程（UWP）
│   ├── VirtualBT.sln
│   └── BluetoothLEExplorer/
│       ├── Models/            # VirtualKeyboard / SafeDispatcher / Loc …
│       ├── Views/             # 页面（Discover / VirtualKeyboard / MouseCapture …）
│       ├── ViewModels/
│       └── Resources/         # zh-CN / en-US 资源表
├── GattServicesLibrary/       # GATT 服务库
├── SortedObservableCollection/
├── NXBT/                      # Switch 手柄模拟（仅 Linux）
└── JoyControl/                # Switch 手柄模拟（仅 Linux）
```

---

## 从源码构建

> 普通使用无需构建，直接运行 `启动VirtualBT.bat` 即可。

### 工具链

需要 **Visual Studio Community 2022**，并勾选：

- `Universal Windows Platform development`（`Microsoft.VisualStudio.Workload.Universal`）
- Windows 10 SDK 10.0.19041
- .NET Native 编译器

已验证可用的完整安装命令：

```powershell
vs_community.exe --add Microsoft.VisualStudio.Workload.Universal `
  --add Microsoft.VisualStudio.Component.Windows10SDK.19041 `
  --add Microsoft.VisualStudio.Component.DotNetNative --quiet --norestart --wait
```

> Visual Studio Build Tools 2022 **不包含** UWP 工作负载，即使安装也无法编译此工程。

### 编译

```bash
MSBuild.exe BluetoothLEExplorer/BluetoothLEExplorer/VirtualBT.csproj \
  -t:Build -p:Configuration=Release -p:Platform=x86 \
  -p:UseDotNetNativeToolchain=true \
  -p:TargetFrameworkRootPath=D:/VirtualBT-main/deploy/refasm
```

`TargetFrameworkRootPath` 指向 `deploy/refasm/`，因为 `.NETCore v5.0` 引用程序集未随 Visual Studio 安装，需从 NuGet 的 `ref/netcore50` 汇集。

### 部署

```bash
# 产物在 bin/x86/Release/ilc/
rm -rf deploy/AppX
cp -r BluetoothLEExplorer/BluetoothLEExplorer/bin/x86/Release/ilc deploy/AppX

# 重新注册
powershell -Command "Get-AppxPackage -Name 'Microsoft.BluetoothLEExplorer' | Remove-AppxPackage; \
  Add-AppxPackage -Register 'D:\VirtualBT-main\deploy\AppX\AppxManifest.xml'"
```

依赖包（`deploy/nuget/`）若未安装：

```powershell
Add-AppxPackage -Path deploy\nuget\Microsoft.NET.Native.Runtime.1.6.appx
Add-AppxPackage -Path deploy\nuget\Microsoft.NET.Native.Framework.1.6.appx
```

---

## 已知限制

- **鼠标捕获**基于位置差值推算相对位移；指针到达屏幕边缘后会停止（见排错）。
- **按键仅在应用窗口获得焦点时转发**（游戏模式下为全屏独占，不受此限制）。
- **Switch 手柄模拟**（`NXBT` / `JoyControl`）依赖 Linux BlueZ，在 Windows 上不可用。
- 尚未实现**游戏手柄** HID 报文（当前为键盘与鼠标）。

---

## 开发文档

实现细节与问题记录见 **[docs/开发笔记.md](docs/开发笔记.md)**（[English](docs/developer-notes.md)）：

- Win11 24H2+ 蓝牙扫描闪退的根因与修复方法
- 本地化（中英切换）的三个问题
- HID 键鼠报文格式
- 构建环境细节

## 参与改进

可能的方向：

- 使用 `InputInjector` 在屏幕中心回弹，实现“无限”鼠标移动
- HID 手柄、触摸板报文
- 鼠标 DPI / 加速度曲线配置
- 开机自启、系统托盘常驻

---

## 原项目与引用项目

本仓库是**上游 VirtualBT 项目的修改版**。完整的许可证归属见 [NOTICE](NOTICE)。

### 原项目 / 上游

| 项目 | 地址 | 许可证 |
|---|---|---|
| **VirtualBT**（本项目来源） | https://github.com/itsmikethetech/VirtualBT | GPL-3.0 |
| VirtualBT（组织镜像） | https://github.com/VirtualDrivers/VirtualBT | GPL-3.0 |

相对上游的改动见 [CHANGELOG.md](CHANGELOG.md)。

### 引用 / 内嵌项目

| 项目 | 地址 | 许可证 | 用途 |
|---|---|---|---|
| BluetoothLEExplorer（微软） | https://github.com/microsoft/BluetoothLEExplorer | MIT | UWP GATT 调试框架、GATT 服务库、集合控件 |
| NXBT（Brikwerk） | https://github.com/Brikwerk/nxbt | MIT | Switch 手柄模拟（内嵌于 `NXBT/`） |
| joycontrol（mart1nro） | https://github.com/mart1nro/joycontrol | GPL-3.0 | Switch 手柄协议（内嵌于 `JoyControl/`） |
| Nintendo_Switch_Reverse_Engineering（dekuNukem） | https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering | 见上游 | Switch 协议逆向文档 |

构建时用到的 NuGet 包（微软 .NET Native / UWP）见 [NOTICE](NOTICE)。

## 致谢

本项目的蓝牙相关知识主要来自上述项目，谨向相关作者致谢。

- [microsoft/BluetoothLEExplorer](https://github.com/microsoft/BluetoothLEExplorer) —— UWP GATT 调试框架
- [mart1nro/joycontrol](https://github.com/mart1nro/joycontrol) —— Switch 手柄协议
- [Brikwerk/nxbt](https://github.com/Brikwerk/nxbt) —— Switch 手柄模拟
- [dekuNukem/Nintendo_Switch_Reverse_Engineering](https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering) —— Switch 协议逆向文档

---

## 许可

- 本项目代码：GPL-3.0
- JoyControl：GPL-3.0
- Microsoft 提供的代码：MIT
- NXBT：MIT

详见 [LICENSE](LICENSE)。