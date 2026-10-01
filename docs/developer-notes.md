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

## 5. Mouse capture relative motion

`Windows.UI.Input.MouseDevice.MouseMoved` does not resolve in this UWP projection (`CS0246`), so relative movement is derived from **position deltas** on `CoreWindow.PointerMoved`.

**Side effect:** reporting stops when the pointer reaches the screen edge (UWP has no `ClipCursor`). The mitigation is `Windows.UI.Input.Preview.Injection.InputInjector` recentring the pointer, which needs this in `Package.appxmanifest`:

```xml
<restricted:Capability Name="inputInjection" />
```

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

Commands: see [README](../README.md#building-from-source).
