# 更新日志 / Changelog

> 🌐 English sections are marked **EN**; 中文段落标 **中**.

---

## [未发布 / Unreleased] — 2026-10-01

### 新增 / Added

- **中｜蓝牙鼠标模拟**：HID Report ID 2，相对位移（16 位 X/Y）、左/右/中键、滚轮。手机端识别为标准 HID 键鼠复合设备。
  **EN｜Bluetooth mouse emulation**: HID Report ID 2 — relative motion (16-bit X/Y), left/right/middle buttons, wheel. Phones see a standard HID keyboard+mouse combo.

- **中｜全屏游戏模式**（`Views/MouseCapturePage`）：独占全屏 + 隐藏指针，**鼠标与键盘同时捕获**并转发。
  **EN｜Fullscreen game mode** (`Views/MouseCapturePage`): exclusive fullscreen + hidden pointer, **captures mouse and keyboard at the same time**.
  - 中｜`Esc` 退出全屏（不转发给手机） / EN｜`Esc` leaves fullscreen (not forwarded to the phone)
  - 中｜`F1` 显示/隐藏提示条 / EN｜`F1` toggles the hint overlay
  - 中｜小键盘 `+` / `-` 调节灵敏度 0.1x ~ 5.0x / EN｜Numpad `+` / `-` for sensitivity 0.1x – 5.0x
  - 中｜每 8 ms 汇总一次上报，贴合 BLE 连接间隔 / EN｜aggregates reports every 8 ms to match a BLE connection interval
  - 中｜退出时自动发送「全部释放」报告，避免手机端按键/视角卡住 / EN｜sends an all-released report on exit so nothing sticks on the phone

- **中｜中英双语切换**（设置 → 语言）：标准 UWP `.resw` + `x:Uid` 资源化，214 条字符串 × 2 语言；切换后自动重启生效。
  **EN｜English / 中文 switch** (Settings → Language): standard UWP `.resw` + `x:Uid` localisation, 214 strings × 2 languages; applied by restarting the app.

- **中｜一键启动脚本** `启动VirtualBT.bat`：自动安装缺失运行时、注册应用包、启动或置前窗口。
  **EN｜One-click launcher** `启动VirtualBT.bat`: installs missing runtimes, registers the package, starts the app or focuses the window.

- **中｜蓝牙状态感知的错误提示**：`RadioNotAvailable` 时自动检测蓝牙开关状态，给出针对性排错步骤。
  **EN｜Bluetooth-aware error messages**: on `RadioNotAvailable` the app checks the radio state and gives targeted steps.

- **中｜配对失败的详细诊断**：`AccessDenied` / `Rejected` / `Failed` 等状态映射为可操作的中文说明。
  **EN｜Pairing failure diagnostics**: maps `AccessDenied` / `Rejected` / `Failed` etc. to actionable explanations.

### 修复 / Fixed

- **中｜扫描页点「开始」闪退**（Win11 24H2+，`0x800710DF` / `combase.dll`）：
  **EN｜Crash when pressing Start on the scan page** (Win11 24H2+, `0x800710DF` / `combase.dll`):
  - 中｜新增 `Models/SafeDispatcher.cs`，缓存 UI 线程 `CoreDispatcher`，替换 20 处 `CoreApplication.MainView.CoreWindow.Dispatcher` 后台线程调用（该访问在 Win11 24H2+ 会 fail-fast）
    **EN｜Added `Models/SafeDispatcher.cs`** caching the UI-thread `CoreDispatcher`; replaced 20 background-thread uses of `CoreApplication.MainView.CoreWindow.Dispatcher` (which fail-fasts on Win11 24H2+)
  - 中｜同时替换 Template10 `Dispatcher.DispatchAsync` 的 4 处调用（框架内部也用了同样的危险 API）
    **EN｜Also replaced Template10's `Dispatcher.DispatchAsync`** at 4 sites — the framework itself uses the same dangerous API
  - 中｜`BluetoothLEAdvertisementWatcher` 不再 `Stop()` 后复用同一个实例 `Start()`（与 WinRT 任务队列销毁竞态），改为每次新建
    **EN｜`BluetoothLEAdvertisementWatcher` is no longer `Stop()`ed and reused** (races with WinRT task-queue teardown); a fresh instance is created each time
  - 中｜为 `AdvertisementWatcher_Received` 增加 sender 校验，忽略已销毁 watcher 的迟到回调
    **EN｜`AdvertisementWatcher_Received` validates `sender`** and drops late callbacks from torn-down watchers
  - 中｜修复 `SemaphoreSlim.Release()` 在 `finally` 中无条件调用（`WaitAsync` 未成功时会抛异常并杀死 `async void`）
    **EN｜Fixed unconditional `SemaphoreSlim.Release()` in `finally`** (throws when `WaitAsync` never ran, killing the `async void`)
  - 中｜包裹 `deviceWatcher.Stop()` 与扫描启动的 try/catch
    **EN｜Wrapped `deviceWatcher.Stop()` and watcher start in try/catch**

- **中｜`Stretch="None"` 被误译导致 XAML 编译失败**（枚举值不能本地化）
  **EN｜`Stretch="None"` mistranslated broke the XAML compile** — enum values must not be localised

- **中｜语言设置被 `IsSelected="True"` 覆盖**：`ComboBoxItem` 的 `IsSelected` 在 `InitializeComponent` 期间触发 `SelectionChanged`，把刚保存的 `en-US` 又写回 `zh-CN`
  **EN｜`IsSelected="True"` clobbered the saved language**: `ComboBoxItem.IsSelected` fires `SelectionChanged` during `InitializeComponent`, writing `zh-CN` back over a just-saved `en-US`

### 变更 / Changed

- 中｜错误提示、弹窗消息全部接入资源表，随语言切换
  **EN｜All error and dialog messages go through the resource tables** and follow the language switch
- 中｜`VirtualKeyboard.cs` 的 HID 服务创建增加 6 次重试（每次 1 秒），缓解瞬时的广播资源占用
  **EN｜HID service creation retries 6 times** (1 s apart) to ride out transient advertising-resource contention

---

## 历史 / Earlier

### 基础版本 / Base version（源自上游 / from upstream）

- 中｜基于 [microsoft/BluetoothLEExplorer](https://github.com/microsoft/BluetoothLEExplorer) 的 UWP GATT 调试框架
  **EN｜Based on the [microsoft/BluetoothLEExplorer](https://github.com/microsoft/BluetoothLEExplorer) UWP GATT explorer framework**
- 中｜虚拟蓝牙键盘（HID over GATT，Report ID 1）
  **EN｜Virtual Bluetooth keyboard (HID over GATT, Report ID 1)**
- 中｜BLE 设备扫描、应用内配对、特征值读写
  **EN｜BLE scanning, in-app pairing, characteristic read/write**
- 中｜广播信标、广播监听
  **EN｜Advertise beacons, advertisement monitoring**
- 中｜基于 `GattServicesLibrary` / `SortedObservableCollection`
  **EN｜Built on `GattServicesLibrary` / `SortedObservableCollection`**
