# 更新日志 / Changelog

> 🌐 English sections are marked **EN**; 中文段落标 **中**.

---

## [1.2.0] — 2026-10-01

游戏模式（鼠标捕获）的三项改进：键盘完全隔离、移动范围放开、退出快捷键可自定义。
Three changes to game mode (mouse capture): full keyboard isolation, unbounded pointer movement, and a configurable exit shortcut.

### 变更 / Changed

- **中｜捕获模式下键盘不再影响电脑端窗口**：除退出快捷键外的任何按键（含 `Ctrl+S` 等组合快捷键、`Tab` 焦点切换、文本输入、访问键）都不会再作用于本应用窗口，只转发给手机。
  仅拦截 `CoreWindow.KeyDown` 不足以挡住 XAML 自身的按键处理；现在在 `CoreWindow.Dispatcher.AcceleratorKeyActivated`（及 `CharacterReceived`）上置 `Handled = true`，按键仍经 `CoreWindow.KeyDown/KeyUp` 按 `KeyStatus.ScanCode` 转发，转发逻辑不变。
  **EN｜Keyboard input no longer affects the PC window during capture**: every key except the exit shortcut — including chords such as `Ctrl+S`, `Tab` focus moves, text input and access keys — is kept away from this app's window and only forwarded to the phone.
  Intercepting `CoreWindow.KeyDown` alone does not stop XAML's own key handling; `CoreWindow.Dispatcher.AcceleratorKeyActivated` (and `CharacterReceived`) now set `Handled = true`. Keys are still forwarded through `CoreWindow.KeyDown/KeyUp` using `KeyStatus.ScanCode`, so forwarding itself is unchanged.
  - 中｜释放按键只对**按下时确实转发过**的键执行，本地吞掉的键（退出快捷键、`F1`、小键盘 `+/-`）不会在手机端产生按下无释放的卡键。离开捕获模式时 `VirtualKeyboard.ReleaseAllKeys()` 统一释放，退出快捷键带的修饰键（如 `Ctrl+E` 的 `Ctrl`）也不会残留在手机上。
    **EN｜A key release is only forwarded for keys whose press was actually forwarded**, so a locally-consumed key (exit shortcut, `F1`, numpad `+/-`) can no longer leave a stuck key on the phone. `VirtualKeyboard.ReleaseAllKeys()` runs when leaving capture mode, so the exit chord's modifier (e.g. `Ctrl` in `Ctrl+E`) is released too.
  - 中｜`F1` 显示/隐藏提示时会**返回**而不再转发到手机。此前按下会转发而释放被吞掉，等于在手机上留下一个按住不放的 `F1`。
    **EN｜`F1` toggling the hint now returns instead of being forwarded.** Previously the press was forwarded while the release was swallowed, leaving `F1` stuck down on the phone.

- **中｜鼠标模拟的移动范围不再受窗口/屏幕大小限制，也不会再呼出标题栏**：指针接近窗口或屏幕边缘（48 DIP）时自动弹回所在显示器中央，视角可以持续转动；弹回产生的跳变不计入位移。弹回经 `user32!SetCursorPos` 精确定位，不经过鼠标加速度缩放。全屏 chrome 设为 `Minimal`，并尝试 `Hidden`（读回确认），指针顶到屏幕上边缘不再带出标题栏。
  若 user32 不可用则退回 `InputInjector` 绝对定位（需清单声明 `rescap:Capability Name="inputInjection"`，受限能力，侧载可用）；两者都不可用时进入捕获模式会提示「无法移动系统指针」，行为与旧版一致。状态行会显示当前生效的弹回方式（`回中 win32` / `回中 injector` / `回中 none`）。
  **EN｜Mouse-simulation movement is no longer limited by the window/screen size, and the title bar no longer appears**: when the pointer comes within 48 DIPs of a window or screen edge it is warped to the centre of its monitor, so the camera can keep turning; the jump itself is not counted as movement. The warp goes through `user32!SetCursorPos` and lands exactly, with no mouse-ballistics scaling. The fullscreen chrome is set to `Minimal`, with `Hidden` attempted and read back, so a pointer at the top edge can no longer summon the title bar.
  If user32 is unavailable the code falls back to absolute `InputInjector` positioning (requires `rescap:Capability Name="inputInjection"` in the manifest — a restricted capability, fine when sideloaded). If neither works, entering capture mode shows “Cannot move the system pointer” and behaviour matches the previous version. The status line always shows which warp is active (`回中 win32` / `回中 injector` / `回中 none`).

### 新增 / Added

- **中｜进入鼠标捕获模式时自动隐藏左侧导航栏**：此前导航栏仍占着窗口左侧——捕获区域铺不满屏幕，指针移到导航按钮上时移动不再转发（点击还会直接导航离开捕获页）。现在进入捕获模式会收起导航面板与汉堡按钮，捕获页铺满整个窗口；退出捕获后原样恢复。
  **EN｜The left navigation bar is hidden while mouse capture is active**: it used to keep its strip of the window — the capture area did not fill the screen, and pointer movement stopped being forwarded while the pointer was over a nav button (clicking one even navigated away from the page). Entering capture mode now collapses the nav pane and hamburger button so the capture page fills the window; leaving restores both exactly.

- **中｜游戏模式全屏方式可切换（独占全屏 / 无边框窗口全屏）**：设置页新增「游戏模式全屏」。部分系统上无边框窗口全屏时指针顶到屏幕上方仍会带出标题栏，且系统浮层无法完全关闭；现在可改用**独占全屏**——把顶层窗口（UWP 应用由 `ApplicationFrameHost` 托管）改为无边框置顶并铺满整块屏幕，标题栏与最小化/最大化/关闭按钮一并移除，顶部**没有任何可呼出的系统界面**，也不会误点到窗口按钮。
  独占全屏失败时自动回退到无边框窗口全屏（回退时状态行会标明），两种模式都支持退出快捷键与指针弹回。设置即时生效，下次进入捕获模式时应用，无需重启。
  **EN｜Game-mode fullscreen is switchable (exclusive / borderless window)**: Settings now has “Game-mode fullscreen”. On some systems the borderless-window fullscreen still summons a title bar from the top edge and the system overlay cannot be turned off completely; **Exclusive** can be used instead — the top-level window (UWP apps are hosted by `ApplicationFrameHost`) is turned into a borderless topmost window covering the whole monitor, with the caption and minimize/maximize/close buttons removed, so there is nothing at the top edge to summon and no window buttons to click by accident.
  If exclusive mode cannot be applied the app falls back to borderless-window fullscreen (the status line says so). Both modes support the exit shortcut and the pointer warp. The setting takes effect the next time capture mode is entered; no restart is needed.

- **中｜退出鼠标捕获的快捷键可自定义**：设置页新增「退出鼠标捕获」，点击按钮后按下目标键（可含 `Ctrl` / `Alt` / `Shift`）即完成绑定，保存在 `LocalSettings`，「恢复默认」回到 `Esc`。捕获页提示条会显示当前快捷键。默认仍为 `Esc`，与旧版一致。
  **EN｜The mouse-capture exit shortcut is configurable**: Settings now has “Exit mouse capture” — click the button, press the desired key (optionally with `Ctrl` / `Alt` / `Shift`) to bind it. The choice is stored in `LocalSettings`; “Reset to default” returns to `Esc`. The capture-page hint shows the current shortcut. The default is still `Esc`, matching previous versions.

### 变更 / Changed

- 中｜包版本号变更为 **1.2.0.0**。
  **EN｜Package version changed to 1.2.0.0**.

---

## [1.1.0] — 2026-10-01

本节为「连接手机后闪退」问题的根因定位与最终修复。v1.0.1 所含改动属实，但并非该问题的成因。
This section documents the root cause of the crash on phone connection and its final fix. The changes in v1.0.1 are genuine, but they were not the cause of that issue.

### 修复 / Fixed

- **中｜连接手机后闪退**（`0xc000027b` / `Windows.UI.Xaml.dll`，所有 Windows 11 版本）：真实异常为 **`System.InvalidCastException`**，发生于 x:Bind 编译绑定生成的 `VirtualKeyboardPage_obj13_Bindings.Update_IsConnected`。
  「已连接 / 未连接」状态文本经 `ValueWhenConverter` 取值 `LocalizedString` 对象；x:Bind 会将转换结果强制转换为 `string` 后赋给 `TextBlock.Text`。类型不符，转换抛出异常，进程随即终止。
  **EN｜Crash on phone connection** (`0xc000027b` / `Windows.UI.Xaml.dll`, all Windows 11 builds): the actual exception is **`System.InvalidCastException`**, raised in the x:Bind compiled binding `VirtualKeyboardPage_obj13_Bindings.Update_IsConnected`.
  The "Connected / Disconnected" text is produced by a `ValueWhenConverter` whose value is a `LocalizedString` object; x:Bind casts the converter result to `string` before assigning it to `TextBlock.Text`. The type mismatch makes the cast throw, and the process terminates.
  - 中｜`ObservableGattClient` 新增 `ConnectionText` 属性（`string` 类型，经 `Loc.Get` 读取资源），绑定改为 `Text="{x:Bind ConnectionText}"`。绑定目标为字符串属性，不再经过转换，因而不产生强制转换；中英文文案保持不变。
    **EN｜`ObservableGattClient` now exposes `ConnectionText`** (a `string` looked up with `Loc.Get`), bound as `Text="{x:Bind ConnectionText}"`. The target is a string property, so no conversion takes place and no cast is produced; the Chinese and English wording is unchanged.
  - 中｜扫描页的「开始 / 停止」按钮采用相同写法，但绑定目标为 `Button.Content`（`object` 类型），对象可直接接受并通过 `ToString()` 显示，故此前未暴露该问题。`LocalizedString` 的这一限制已在注释中说明。
    **EN｜The scan page's Start/Stop buttons use the same pattern but bind to `Button.Content` (`object`)**, which accepts the object and renders it via `ToString()` — which is why the issue had not surfaced before. This limitation is now documented on `LocalizedString`.

### 修复 / Fixed（其他隐患 / other hazards）

- **中｜`ObservableGattClient` 的终末器在 .NET 终末器线程上调用 WinRT**（`Dispose()`、取消事件订阅）：该线程调用 WinRT 会从 combase 抛出 `E_NOTIMPL`，与错误报告中的 `0x80004002` 吻合。现已移除终末器——设备对象与包装对象互相引用，会被一并回收，无需终结。
  **EN｜`ObservableGattClient`'s finalizer called into WinRT** (`Dispose()`, event unsubscribe) from the .NET finalizer thread, which throws `E_NOTIMPL` out of combase — matching `0x80004002` in the error report. The finalizer is removed: the device and its wrapper reference each other and are collected together, so no finalization is required.
- **中｜两处 WinRT 事件回调增加异常保护**：`HidKeyboardReport_SubscribedClientsChanged` 与 `ConnectionStatusChanged` 原先未捕获异常，异常逃出后被记为 `0xc000027b` 并终止进程。
  **EN｜Two WinRT event handlers now guard against exceptions**: `HidKeyboardReport_SubscribedClientsChanged` and `ConnectionStatusChanged` previously let exceptions escape, where they were recorded as `0xc000027b` and terminated the process.
- **中｜`HidControlPoint_WriteRequested`**：`GetRequestAsync` 改为在 UI 线程调用（该 API 要求 UX 线程），并将 `deferral.Complete()` 移入 `finally`；请求失败时不再阻塞主机端的 ATT 写入。
  **EN｜`HidControlPoint_WriteRequested`**: `GetRequestAsync` now runs on the UI thread (the API requires the UX thread) and `deferral.Complete()` is in a `finally`, so a failed request no longer blocks the central's ATT write.

### 变更 / Changed

- **中｜全局未捕获异常诊断**：接入 `CoreApplication.UnhandledErrorDetected`，WinRT 回调中的异常会写入应用本地目录的 `unhandled-error.log` 并被拦截，进程不再终止；XAML 的 `UnhandledException` 同样记录并置为已处理。本次根因即由该日志定位。
  **EN｜Global unhandled-error diagnostics**: `CoreApplication.UnhandledErrorDetected` writes WinRT callback exceptions to `unhandled-error.log` in the app's local folder and intercepts them, so the process survives; XAML's `UnhandledException` is likewise logged and marked handled. The root cause was located through this log.
- 中｜包版本号变更为 **1.1.0.0**。
  **EN｜Package version changed to 1.1.0.0**.

---

## [1.0.1] — 2026-10-01

> 中｜本节改动属实，但**并非闪退的原因**；真正根因见 [1.1.0]。
> **EN｜These changes are real, but they were not the cause of the crash** — see [1.1.0] for the actual root cause.

### 修复 / Fixed

- **中｜连接手机后闪退**（`0xc000027b` / `Windows.UI.Xaml.dll`，Win11 24H2+）：手机连上后会读取 HID 服务特征值（Report Map、HID Information 等），触发 GATT 读/写回调；而 `GattServicesLibrary/GenericGattCharacteristic.cs` 仍在后台线程调用 `CoreApplication.MainView.CoreWindow.Dispatcher`。与扫描闪退同一根因（见 1.0.0），上次只修了应用工程里的 20 处，下层库漏了。
  **EN｜Crash as soon as a phone connects** (`0xc000027b` / `Windows.UI.Xaml.dll`, Win11 24H2+): connecting makes the phone read the HID service characteristics (Report Map, HID Information, …), which fires the GATT read/write callbacks in `GattServicesLibrary/GenericGattCharacteristic.cs` — those still called `CoreApplication.MainView.CoreWindow.Dispatcher` from a thread-pool thread. Same root cause as the scan crash (see 1.0.0); the earlier fix covered the 20 sites in the app project but missed the lower-layer library.
  - 中｜`SafeDispatcher` 下沉到 `GattServicesLibrary.Helpers`，GATT 回调与应用共用同一份缓存；`BluetoothLEExplorer.Models.SafeDispatcher` 保留为门面，现有调用点不变。
    **EN｜`SafeDispatcher` moved down into `GattServicesLibrary.Helpers`** so the GATT callbacks share the same cache; `BluetoothLEExplorer.Models.SafeDispatcher` remains as a facade, so existing call sites are unchanged.
  - 中｜读/写请求的 `deferral.Complete()` 移入 `finally`：请求失败也不再悬挂，手机端的 ATT 事务不会卡死。
    **EN｜`deferral.Complete()` moved into `finally`**: a failed request can no longer hang the phone's ATT transaction.

- **中｜订阅变化回调的两处隐患**：`SubscribedHidClientsChanged` 在线程池线程直接 `RaisePropertyChanged`（XAML 绑定非 UI 线程更新）；`ObservableGattClient.FromIdAsync` 在设备查找失败时用 null 构造，`async void` 未捕获直接闪退。现已跳转 UI 线程通知、空值跳过、整体 try/catch。
  **EN｜Two hazards in the subscribed-clients callback**: `SubscribedHidClientsChanged` raised `PropertyChanged` straight from a thread-pool thread (XAML binding update off the UI thread), and `ObservableGattClient.FromIdAsync` constructed around null when the device lookup failed — uncaught inside an `async void`, so the process died. Now marshalled to the UI thread, nulls skipped, whole handler guarded.

### 变更 / Changed

- 中｜启动脚本每次启动都刷新注册（`Add-AppxPackage -Register`）。原地覆盖应用文件后旧注册是失效的，应用会静默拒绝启动；注册指向别的目录或版本不一致时（例如解压了新版到新目录）先移除再注册，避免启动到旧构建。
  **EN｜The launcher re-registers on every start** (`Add-AppxPackage -Register`). Replacing files under a registered loose package leaves the registration stale and the app silently refuses to start; when the registration points at a different folder or a different version (e.g. a newer copy extracted elsewhere) it is removed first so the stale build cannot be launched.
- 中｜包版本号从上游的 `1.16.3.0` 改为与发布号一致的 `1.0.1.0`，便于在 Windows 设置里确认装的是哪一版。
  **EN｜The package version is now `1.0.1.0`**, matching the release number, so the installed build is identifiable in Windows Settings.

---

## [1.0.0] — 2026-10-01

首次发布 / First release of this fork.

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

- **中｜一键启动脚本** `启动VirtualBT.bat`：自动安装缺失运行时（.NET Native 1.6、`Microsoft.VCLibs.x86.14.00`）、注册应用包、启动或置前窗口。
  **EN｜One-click launcher** `启动VirtualBT.bat`: installs missing runtimes (.NET Native 1.6, `Microsoft.VCLibs.x86.14.00`), registers the package, starts the app or focuses the window.

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
