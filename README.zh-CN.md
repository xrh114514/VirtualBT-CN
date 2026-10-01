# VirtualBT

**Windows 虚拟蓝牙输入设备** —— 让电脑变成一台蓝牙键盘 / 鼠标，把键鼠操作转发到手机、平板等设备上。

典型用法：手机玩 FPS 游戏时，用电脑的鼠标转视角、键盘走位，比搓玻璃舒服得多。

界面支持**中文 / English** 一键切换。

> 🌐 **English**: [README.md](README.md)
>
> 📖 **只想用的话**，看 [快速上手.md](快速上手.md)；详细排错见下方[疑难排错](#疑难排错)。
>
> 🔗 **原项目**：[itsmikethetech/VirtualBT](https://github.com/itsmikethetech/VirtualBT) · [VirtualDrivers/VirtualBT](https://github.com/VirtualDrivers/VirtualBT)

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
| ⌨️ **蓝牙键盘模拟** | 电脑按键实时转发到已连接设备（BLE HID） |
| 🖱️ **蓝牙鼠标模拟** | 相对位移、左/右/中键、滚轮（BLE HID，16 位精度） |
| 🎮 **全屏游戏模式** | 独占全屏 + 隐藏指针，**鼠标和键盘同时捕获**，适合转视角 |
| 🔍 **BLE 设备扫描与配对** | 扫描周围低功耗蓝牙设备、应用内配对 |
| 🌐 **中英双语** | 设置页一键切换，自动重启生效 |
| 🎛️ **GATT 服务调试** | 查看/读写特征值、广播信标、广播监听等（源自 BluetoothLEExplorer） |

---

## 快速开始

### 1. 一键启动

双击 **`启动VirtualBT.bat`**

脚本会自动：
- 检查并安装缺失的 .NET Native 1.6 运行时
- 注册应用包
- 启动程序（已在运行则把窗口调到前台）

成功时窗口自动关闭；出错时保留窗口显示错误信息。

### 2. 把电脑变成蓝牙键盘 / 鼠标

1. 确认系统**蓝牙已打开**（任务栏快捷设置）
2. 打开 VirtualBT → 左侧菜单 → **虚拟蓝牙键盘**
3. 打开 **「开启广播：可被发现且可连接」** 开关
4. 在手机的蓝牙设置里搜索并配对你的电脑
5. 连接成功后，「已连接的客户端列表」会出现该设备

此时电脑窗口获得焦点时，**键盘输入就会转发到手机**。

### 3. 游戏模式（鼠标 + 键盘全屏捕获）

1. 确认手机已连接（列表里有设备）
2. 点 **「进入鼠标捕获模式（全屏）」**
3. 进入黑底全屏，鼠标移动 = 转视角，键盘 = 走位/开枪

| 按键 | 作用 |
|---|---|
| **`Esc`** | 退出全屏（不转发给手机） |
| **`F1`** | 显示/隐藏提示条 |
| **`小键盘 +` / `-`** | 灵敏度 0.1x ~ 5.0x |
| 其它任意键 | 全部转发给手机（含 `Tab`、`WASD`、`Shift`…） |

### 4. 切换语言

**设置 → 语言 → 中文 / English** → 应用自动重启后生效。

---

## 系统要求

- Windows 10 版本 1903+（内部版本 18362+），已验证 Windows 11
- 支持 **BLE 外设（Peripheral）角色**的蓝牙适配器
  - 常见 Intel 无线蓝牙（AX 系列）需较新驱动
  - 不确定的话，先试一下；报 `RadioNotAvailable` 见下方排错
- 开发者模式 / 允许侧载应用（安装包时会用到）

---

## 疑难排错

### `无法创建HID服务提供程序：RadioNotAvailable`

蓝牙**广播资源不可用**，键盘鼠标都发不出去。应用会自动重试 6 次，并给出诊断提示。按顺序试：

1. **确认系统蓝牙已打开**（最常见原因）
2. 关闭**手机连接 / Phone Link** 等占用蓝牙的程序
3. 蓝牙关掉，等 5 秒再打开
4. 设备管理器 → 蓝牙 → `英特尔(R) 无线 Bluetooth(R)` → 右键「禁用」→「启用」
5. 更新 Intel 无线蓝牙驱动
6. 重启电脑

如果都无效，说明这块网卡**不支持外设角色**，换一个支持 BLE Peripheral 的 USB 蓝牙适配器（如 CSR8510 或较新的 Realtek BLE 5.x）。

### 手机搜不到电脑

- 电脑端开关没打开（「开启广播：可被发现且可连接」）
- 手机蓝牙设置里手动「搜索设备」，别只看快捷面板
- 部分机型需关开一次手机蓝牙才能发现 HID 设备

### 「扫描与配对设备」页点「配对」提示没有权限

正常。**手机一般不作为蓝牙外设被电脑连接**。要用电脑当键盘给手机用，请走「虚拟蓝牙键盘」页 + **手机端配对**，方向是反的。

### 鼠标转到屏幕边缘就停

UWP 没有鼠标锁定 API，指针到达屏幕边界时不再上报相对位移。单屏全屏玩一般够用。

### 切换语言后没生效

设置页切换会保存选择并自动重启。若无效，检查 Windows 设置里应用的语言权限。

---

## 目录结构

```
VirtualBT-main/
├── 启动VirtualBT.bat          # 一键启动（推荐入口）
├── launch-virtualbt.ps1       # 启动逻辑（依赖安装 / 注册 / 启动 / 置前）
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

> 普通使用不需要构建，直接双击 `启动VirtualBT.bat`。

### 工具链

需要 **Visual Studio Community 2022** 并勾选：

- `Universal Windows Platform development`（`Microsoft.VisualStudio.Workload.Universal`）
- Windows 10 SDK 10.0.19041
- .NET Native 编译器

已验证可工作的完整安装命令：

```powershell
vs_community.exe --add Microsoft.VisualStudio.Workload.Universal `
  --add Microsoft.VisualStudio.Component.Windows10SDK.19041 `
  --add Microsoft.VisualStudio.Component.DotNetNative --quiet --norestart --wait
```

> Visual Studio Build Tools 2022 **不含** UWP 工作负载，装了也编不了这个工程。

### 编译

```bash
MSBuild.exe BluetoothLEExplorer/BluetoothLEExplorer/VirtualBT.csproj `
  -t:Build -p:Configuration=Release -p:Platform=x86 `
  -p:UseDotNetNativeToolchain=true `
  -p:TargetFrameworkRootPath=D:/VirtualBT-main/deploy/refasm
```

`TargetFrameworkRootPath` 指向 `deploy/refasm/`，因为 `.NETCore v5.0` 引用程序集没有随 VS 装上，是从 NuGet 的 `ref/netcore50` 汇集而来的。

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

- **鼠标捕获**是位置差值推算的相对位移，指针到屏幕边缘会停（见排错）
- **按键只在应用窗口获得焦点时转发**（游戏模式下是全屏独占，不受影响）
- **Switch 手柄模拟**（`NXBT` / `JoyControl`）依赖 Linux BlueZ，Windows 上不可用
- 尚未实现**游戏手柄** HID 报文（目前是键盘 + 鼠标）

---

## 开发文档

实现细节与踩坑记录见 **[docs/开发笔记.md](docs/开发笔记.md)**（[English](docs/developer-notes.md)）：
- Win11 24H2+ 蓝牙扫描闪退的根因与修法
- 本地化（中英切换）的三个坑
- HID 键鼠报文格式
- 构建环境细节

## 参与改进

可能的方向：

- `InputInjector` 屏幕中心回弹，实现"无限"鼠标移动
- HID 手柄 / 触摸板报文
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

本项目的蓝牙知识几乎全部来自上表这些项目，感谢各位作者。

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
