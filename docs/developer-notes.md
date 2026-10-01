# Developer Notes

> 🇨🇳 中文版：[开发笔记.md](开发笔记.md) · Back to [README](../README.md)

Pitfalls hit while building this. Read before changing code.

---

## 1. `CoreApplication.MainView` fail-fast on Windows 11 24H2+

**Symptom:** pressing **Start** on the scan page kills the process after ~5 s. WER reports `combase.dll` + `0x800710DF` (`ERROR_NO_TASK_QUEUE`).

**Cause:** touching

```csharp
Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher
```

from a **thread-pool thread** fail-fasts on Windows 11 24H2+ (build 26200) — no exception is even thrown. BLE watcher callbacks (`Received` / `Added`) run on thread-pool threads, and the original code had 20 such call sites. **Template10's `Dispatcher.DispatchAsync` does the same thing internally**, so patching only the project code is not enough.

**Fix:** `Models/SafeDispatcher.cs` caches the UI-thread `CoreDispatcher` once; every thread uses `SafeDispatcher.Current` / `SafeDispatcher.RunAsync`. It must be initialized in `App.OnInitializeAsync` via `SafeDispatcher.Initialize(Window.Current.Dispatcher)` — before any watcher callback can fire.

**Note:** `SafeDispatcher.cs` must be listed explicitly in `VirtualBT.csproj`'s `<Compile>` items (old-style csproj does not glob).

---

## 2. A Bluetooth watcher cannot be `Stop()`ed and reused

**Symptom:** intermittent crash after enabling "Continuous Enumeration", same `0x800710DF`.

**Cause:** calling `Stop()` then immediately `Start()` on the same `BluetoothLEAdvertisementWatcher` races with WinRT's internal task-queue teardown.

**Fix:** `StartAdvertisementWatcher` creates a **fresh** watcher each time; `StopAdvertisementWatcher` unsubscribes handlers, calls `Stop()`, then nulls the field. `AdvertisementWatcher_Received` checks `sender` is still the current watcher and drops late callbacks.

---

## 3. Never `Release()` a `SemaphoreSlim` unconditionally in `finally`

In an `async void` handler, if code before `WaitAsync` throws, a `Release()` in `finally` on a semaphore that was never acquired throws `SemaphoreFullException` and kills the process.

**Fix:** a `bool lockTaken` flag; release only when actually acquired.

---

## 4. Three localization traps

### 4.1 `x:Uid` does not override a literal

`x:Uid="Str_X"` plus `Str_X.Text` in `Resources.resw` **does not override** `Text="Discover"`. The literal attribute must be removed so only `x:Uid` remains.

### 4.2 Resource keys compile to `uid/Property` (slash)

`ResourceLoader.GetString("Str_X.Text")` returns an empty string; `GetString("Str_X/Text")` works. `Models/Loc.Get` does the `.` → `/` substitution.

Verify from inside the package:

```powershell
Invoke-CommandInDesktopPackage -PackageFamilyName <PFN> -AppId 'App' `
  -Command 'powershell.exe' -Args '-NoProfile -File <script>'
# in the script: ResourceLoader.GetForViewIndependentUse().GetString('Str_X/Text')
```

### 4.3 `ComboBoxItem IsSelected="True"` clobbers the saved language

`IsSelected="True"` raises `SelectionChanged` during `InitializeComponent()`, writing the default `zh-CN` back over a just-saved `en-US` on every startup.

**Fix:** set `m_initializing = true` **before** `InitializeComponent()`; drop `IsSelected` from XAML; `ApplyStoredLanguage()` applies only an explicitly saved value — never force a default, or it overwrites the `PrimaryLanguageOverride` set right before a restart.

### 4.4 Also

- **XAML enum attribute values must not be translated** (`Stretch="None"`, `ConverterParameter=Hex`, …) — the XAML compiler breaks or converters stop matching.
- `<x:String>value</x:String>` has no dependency property for `x:Uid` to set; use `Models.LocalizedString` (has a `Value` property) instead.
- Chinese inside `.ps1` must be saved as UTF-8 with BOM or GBK, or PowerShell 5.1 reads it as ANSI and garbles/errors.

---

## 5. Mouse capture: relative motion and keyboard isolation

`Windows.UI.Input.MouseDevice.MouseMoved` does not resolve in this UWP projection (`CS0246`), so relative movement is derived from **position deltas** on `CoreWindow.PointerMoved`.

**Side effect:** reporting stops when the pointer reaches the window/screen edge (UWP has no `ClipCursor`), and a pointer at the top edge summons the fullscreen title-bar overlay.

**Mitigation (v1.2.0):** whenever the pointer comes within 48 DIPs of an edge it is warped back to the middle of its monitor, so the deltas keep flowing and the pointer never pokes the top-edge title-bar overlay. The warp is done with **`user32!SetCursorPos`** (`Models/SystemPointer.cs`), aimed at `MonitorFromPoint(GetCursorPos())`'s centre — exact on any monitor layout, and the target is derived in the same coordinate space user32 speaks, so DIP/physical units do not have to be known. The resulting position jump is swallowed (any change ≥ 120 DIPs while a warp is in flight), with a 400 ms timeout that resyncs the baseline if no jump arrived.

**Do not use injected *relative* moves for this.** `InputInjector` relative `DeltaX/DeltaY` are scaled by the Windows mouse ballistics — measured on a default desktop, a 60-unit request moved the cursor 146 px and a −30-unit request 64 px, an inconsistent ~2–2.5× factor — so a "pull to centre" overshoots out of the window and looks like the warp never happened. Injected *absolute* moves are exact and worth keeping as the fallback: `DeltaX/DeltaY` are normalised 0–65535 against the **primary** display (32768,32768 lands on its centre, verified to the pixel), which is wrong for a secondary monitor but fine when user32 is unavailable.

`InputInjector.TryCreate()` is the fallback and needs this in `Package.appxmanifest`, and `CT_Capabilities` is a **strict sequence** — `rescap:Capability` must come *before* `DeviceCapability` or validation fails with `APPX0501`:

```xml
<rescap:Capability Name="inputInjection" />
<DeviceCapability Name="bluetooth" />
```

When neither mechanism is available the page says so in the overlay and behaves as before (movement stops at the edge). The status line always shows which warp is active (`回中 win32` / `回中 injector` / `回中 none`).

**Fullscreen chrome:** `FullScreenSystemOverlayMode` is set to `Minimal` first (the most the 19041 projection names) and then `Hidden` (value 2) is attempted with a **read-back** — an OS that does not know the value would otherwise keep `Standard`, which is the mode that slides the title bar down when the pointer reaches the top edge. On some systems the overlay still comes up whatever the setting, which is why the presentation itself is switchable (see below).

### 5.1 Two fullscreen presentations (Settings → 游戏模式全屏)

**Borderless (default)** is `ApplicationView.TryEnterFullScreenMode()` — a DWM-composited borderless window filling the screen. The system keeps a hidden title bar that the pointer summons from the top edge.

**Exclusive** is as close to a game's exclusive fullscreen as a UWP XAML app gets (there is no DirectX swapchain to hand to `DXGI::SetFullscreenState`): the top-level window is turned into a borderless topmost window covering the monitor, with `WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MIN/MAXIMIZEBOX` stripped. Nothing to summon, no caption buttons to click mid-game.

Two facts make this non-obvious:

- **A UWP app's visible window is owned by `ApplicationFrameHost.exe`.** The top-level `ApplicationFrameWindow` belongs to another process; the app's own `CoreWindow` is a child of it (`EnumWindows` shows the app has no top-level visible window at all). Styling therefore targets `GetAncestor(ICoreWindowInterop.WindowHandle, GA_ROOT)` — i.e. cross-process `SetWindowLong`. Verified working against the running app: `SetWindowLong` succeeds and the caption bit really goes away (style `0x94CF0000` → `0x94030000`).
- **`ICoreWindowInterop` (`45D64A29-A63E-4CB6-B498-5781D298CB4F`)** is the only way to get the CoreWindow's HWND. A direct C# cast from the projected `CoreWindow` to the `ComImport` interface does not compile (`CS0030`) — cast **via `object`** so it becomes a runtime QueryInterface.

`FullscreenMode.TryEnterExclusive()` reads the style back afterwards and restores the saved style/rect on any failure, and the capture page falls back to Borderless when it returns false. LeaveCaptureMode restores the frame exactly (`LeaveExclusive`) — do not call `ExitFullScreenMode` in that path, `ApplicationView` fullscreen was never entered.

**Keyboard isolation:** while capturing, keys must not reach the PC's own window (shortcuts, Tab focus moves, text). `CoreWindow.KeyDown` is *not* enough — it runs alongside XAML's own handling. The hook that actually blocks XAML is `CoreWindow.Dispatcher.AcceleratorKeyActivated` with `args.Handled = true` (plus `CharacterReceived.Handled`). The exit hotkey is checked in both routes (`ExitCapture` is idempotent). Keys are forwarded from `CoreWindow.KeyDown/KeyUp` (the only place with `KeyStatus.ScanCode`), and only a key whose *press* was forwarded gets its release sent — so a locally-consumed key can never leave a stuck key on the phone. `VirtualKeyboard.ReleaseAllKeys()` runs on the way out for the same reason.

**Shell chrome:** `HideShellChrome()` collapses the Template10 `HamburgerMenu` nav pane while capturing (`HamburgerButtonVisibility = Collapsed`, `DisplayMode` and all three `VisualState*DisplayMode` to `Overlay`, `IsOpen = false`) and restores the saved values on leave. Setting *all three* visual-state display modes matters — the control's code-behind re-applies `DisplayMode` on a width change, so only touching `DisplayMode` would bring the sidebar back. Pointer input is not marked handled anywhere; the sidebar had to go because a camera click on a nav button navigates away mid-capture.

---

## 6. HID report formats

The report map has two collections:

| Report ID | Purpose | Payload (excludes the ID byte) |
|---|---|---|
| 1 | Keyboard | `modifiers(1) + keys(7)` = 8 bytes |
| 2 | Mouse | `buttons(1) + X_lo, X_hi, Y_lo, Y_hi + wheel(1)` = 6 bytes |

Each Report ID gets its own Report Value characteristic plus a Report Reference descriptor (`0x2908`). The payload does **not** contain the Report ID byte.

Keycode conversion: `HidHelper.GetPs2Set1ScanCodeFromStatus` (PS/2 set-1 → HID usage), shared by the keyboard page and the capture page.

`VirtualKeyboard.CapturePageOwnsKeyboard` is a static flag that makes the keyboard page's `CoreWindow` handlers step aside while the capture page is active, so a key is never reported twice.

---

## 7. Build environment

- **VS Community 2022** + `Microsoft.VisualStudio.Workload.Universal` (Build Tools 2022 has no UWP workload)
- `.NETCore v5.0` reference assemblies do not come with VS; they were collected from NuGet `ref/netcore50` into `deploy/refasm/` and passed in with `-p:TargetFrameworkRootPath`
- .NET Native 1.6 runtime appx packages live in `deploy/nuget/` (from `Microsoft.Net.Native.SharedLibrary-x86` 1.6.1 and `Microsoft.Net.Native.Compiler` 1.6.2)
- Build output is `bin/x86/Release/ilc/` — copy to `deploy/AppX` and re-register

Release packaging: `python deploy/pack_dist.py <version>` refreshes `deploy/AppX`, assembles `deploy/dist/VirtualBT-CN-v<version>/` (docs + launchers + stripped `deploy/AppX` + `deploy/nuget` runtime appx) and zips it. It uses Python's `zipfile` on purpose — `Compress-Archive` writes the Chinese filenames without the UTF-8 flag and they arrive garbled.

Commands: see [README](../README.md#building-from-source).
