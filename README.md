# VirtualBT

**Virtual Bluetooth input device for Windows** — turn your PC into a Bluetooth keyboard / mouse and forward keystrokes and mouse movement to a phone, tablet, or anything else that accepts a Bluetooth HID peripheral.

Typical use case: play an FPS on your phone with a real mouse to aim and a keyboard to move, instead of smudging the screen.

The UI supports **English / 中文** with a one-click switch.

> 🇨🇳 **中文文档**：[README.zh-CN.md](README.zh-CN.md) · [快速上手](快速上手.md) · [开发笔记](docs/开发笔记.md)
>
> 🔗 **Upstream project**: [itsmikethetech/VirtualBT](https://github.com/itsmikethetech/VirtualBT) · [VirtualDrivers/VirtualBT](https://github.com/VirtualDrivers/VirtualBT)

---

## Table of contents

- [Features](#features)
- [Quick start](#quick-start)
- [System requirements](#system-requirements)
- [Troubleshooting](#troubleshooting)
- [Repository layout](#repository-layout)
- [Building from source](#building-from-source)
- [Known limitations](#known-limitations)
- [Developer docs](#developer-docs)
- [Contributing ideas](#contributing-ideas)
- [Acknowledgements](#acknowledgements)
- [License](#license)

---

## Features

| Feature | Description |
|---|---|
| ⌨️ **Bluetooth keyboard emulation** | Forward PC keystrokes to the connected device over BLE HID |
| 🖱️ **Bluetooth mouse emulation** | Relative motion, left/right/middle buttons, wheel (BLE HID, 16-bit precision) |
| 🎮 **Fullscreen game mode** | Exclusive fullscreen + hidden pointer, **captures mouse and keyboard at the same time** — made for aiming |
| 🔍 **BLE scanning & pairing** | Scan nearby Bluetooth Low Energy devices and pair in-app |
| 🌐 **English / 中文** | One-click switch in Settings, applied on restart |
| 🎛️ **GATT service explorer** | Read/write characteristics, advertise beacons, monitor advertisements (from BluetoothLEExplorer) |

---

## Quick start

### 1. Launch

Double-click **`启动VirtualBT.bat`**

The script automatically:
- checks for and installs the missing .NET Native 1.6 runtime
- registers the app package
- starts the app (or brings the window to the front if already running)

The window closes itself on success; it stays open on failure so you can read the error.

### 2. Use the PC as a Bluetooth keyboard / mouse

1. Make sure **system Bluetooth is ON** (taskbar quick settings)
2. Open VirtualBT → sidebar → **Virtual Keyboard**
3. Flip the **"Discoverable and Connectable"** switch on
4. On your phone, open Bluetooth settings and pair with your PC
5. The device appears under **"Connected clients"**

From then on, **keystrokes are forwarded to the phone whenever the app window has focus**.

### 3. Game mode (fullscreen mouse + keyboard capture)

1. Make sure the phone is connected (it shows in the client list)
2. Click **"Enter Mouse Capture (Fullscreen)"**
3. Black fullscreen; mouse moves = aim, keyboard = movement/shooting

| Key | Action |
|---|---|
| **`Esc`** | Leave fullscreen (not forwarded to the phone) |
| **`F1`** | Show/hide the hint overlay |
| **`Numpad +` / `-`** | Sensitivity 0.1x – 5.0x |
| Everything else | Forwarded to the phone (including `Tab`, `WASD`, `Shift`, …) |

### 4. Switch language

**Settings → Language → 中文 / English** → the app restarts and applies it.

---

## System requirements

- Windows 10 version 1903+ (build 18362+), verified on Windows 11
- A Bluetooth adapter that supports the **BLE peripheral (GATT server) role**
  - Common Intel Wireless Bluetooth (AX series) needs a recent driver
  - If unsure just try it — see troubleshooting below for `RadioNotAvailable`
- Developer mode / sideloading allowed (needed to register the package)

---

## Troubleshooting

### `无法创建HID服务提供程序：RadioNotAvailable`

The Bluetooth **advertising resource is unavailable** — nothing (keyboard or mouse) can be sent. The app retries 6 times automatically and prints a diagnostic. Try in this order:

1. **Turn system Bluetooth ON** (the most common cause by far)
2. Close **Phone Link / Your Phone** and anything else using Bluetooth
3. Turn Bluetooth off, wait 5 s, turn it back on
4. Device Manager → Bluetooth → *Intel(R) Wireless Bluetooth* → right-click **Disable**, then **Enable**
5. Update the Intel Wireless Bluetooth driver
6. Reboot

If none of it helps, that adapter simply **does not support the peripheral role**. Use a USB Bluetooth dongle that does (e.g. CSR8510 or a recent Realtek BLE 5.x).

### The phone cannot find the PC

- The PC-side "Discoverable and Connectable" switch is off
- Trigger **Scan** manually in the phone's Bluetooth settings — don't rely on the quick panel
- Some phones need their own Bluetooth toggled off/on before they list HID devices

### Clicking "Pair" on the scanning page says "no permission"

Expected. **A phone is generally not a BLE peripheral that a PC connects to.** To use the PC as a keyboard for the phone, go to the **Virtual Keyboard** page and **pair from the phone side** — the direction is the other way around.

### Mouse stops at the screen edge

UWP has no mouse-capture API, so relative motion stops reporting when the pointer hits the border. Fine for single-monitor fullscreen.

### Language switch has no effect

Settings → Language saves the choice and restarts the app. If it still does not apply, check the app's language permissions in Windows Settings.

---

## Repository layout

```
VirtualBT-main/
├── 启动VirtualBT.bat          # One-click launcher (recommended entry point)
├── launch-virtualbt.ps1       # Launcher logic (deps / register / start / focus)
├── deploy/
│   ├── AppX/                  # Deployed app package (build output)
│   ├── nuget/                 # .NET Native 1.6 runtime appx packages
│   └── refasm/                # .NETCore v5.0 reference assemblies for building
├── BluetoothLEExplorer/       # Main project (UWP)
│   ├── VirtualBT.sln
│   └── BluetoothLEExplorer/
│       ├── Models/            # VirtualKeyboard / SafeDispatcher / Loc …
│       ├── Views/             # Pages (Discover / VirtualKeyboard / MouseCapture …)
│       ├── ViewModels/
│       └── Resources/         # en-US / zh-CN string tables
├── GattServicesLibrary/       # GATT service library
├── SortedObservableCollection/
├── NXBT/                      # Switch controller emulation (Linux only)
└── JoyControl/                # Switch controller emulation (Linux only)
```

---

## Building from source

> You don't need this to use the app — just double-click `启动VirtualBT.bat`.

### Toolchain

**Visual Studio Community 2022** with:

- *Universal Windows Platform development* (`Microsoft.VisualStudio.Workload.Universal`)
- Windows 10 SDK 10.0.19041
- .NET Native compiler

Verified install command:

```powershell
vs_community.exe --add Microsoft.VisualStudio.Workload.Universal `
  --add Microsoft.VisualStudio.Component.Windows10SDK.19041 `
  --add Microsoft.VisualStudio.Component.DotNetNative --quiet --norestart --wait
```

> Visual Studio **Build Tools 2022 does not ship the UWP workload** — it cannot build this project.

### Compile

```bash
MSBuild.exe BluetoothLEExplorer/BluetoothLEExplorer/VirtualBT.csproj `
  -t:Build -p:Configuration=Release -p:Platform=x86 `
  -p:UseDotNetNativeToolchain=true `
  -p:TargetFrameworkRootPath=D:/VirtualBT-main/deploy/refasm
```

`TargetFrameworkRootPath` points at `deploy/refasm/` because the `.NETCore v5.0` reference assemblies do not get installed with VS; they were collected from the NuGet `ref/netcore50` folders.

### Deploy

```bash
# Output lands in bin/x86/Release/ilc/
rm -rf deploy/AppX
cp -r BluetoothLEExplorer/BluetoothLEExplorer/bin/x86/Release/ilc deploy/AppX

# Re-register
powershell -Command "Get-AppxPackage -Name 'Microsoft.BluetoothLEExplorer' | Remove-AppxPackage; \
  Add-AppxPackage -Register 'D:\VirtualBT-main\deploy\AppX\AppxManifest.xml'"
```

If the runtime dependencies in `deploy/nuget/` are not installed yet:

```powershell
Add-AppxPackage -Path deploy\nuget\Microsoft.NET.Native.Runtime.1.6.appx
Add-AppxPackage -Path deploy\nuget\Microsoft.NET.Native.Framework.1.6.appx
```

---

## Known limitations

- **Mouse capture** derives relative motion from position deltas, so it stops at the screen edge (see troubleshooting)
- **Keystrokes are only forwarded while the app window has focus** (irrelevant in game mode, which is fullscreen-exclusive)
- **Switch controller emulation** (`NXBT` / `JoyControl`) needs Linux BlueZ — not usable on Windows
- **Gamepad** HID reports are not implemented yet (keyboard + mouse only)

---

## Developer docs

Implementation details and pitfalls live in **[docs/开发笔记.md](docs/开发笔记.md)** (Chinese):

- Root cause of the Win11 24H2+ Bluetooth scan crash (`0x800710DF` / `combase.dll`) and the `SafeDispatcher` fix
- Three localization traps: `x:Uid` never overrides a literal, resource keys compile to `uid/Property` (slash), and `IsSelected="True"` clobbers a saved language
- HID keyboard/mouse report formats
- Build environment quirks (`TargetFrameworkRootPath`, why Build Tools won't do)

Also: **[CHANGELOG.md](CHANGELOG.md)** for what changed.

---

## Contributing ideas

Good next steps:

- `InputInjector` recentre-to-center for "infinite" mouse movement
- HID gamepad / touchpad reports
- Mouse DPI / acceleration curve settings
- Start with Windows, tray icon

---

## Upstream project & referenced projects

This repository is a **modified fork** of the upstream VirtualBT project. Full
licence attribution is in [NOTICE](NOTICE).

### Upstream / origin

| Project | Address | Licence |
|---|---|---|
| **VirtualBT** (this project's origin) | https://github.com/itsmikethetech/VirtualBT | GPL-3.0 |
| VirtualBT (organisation mirror) | https://github.com/VirtualDrivers/VirtualBT | GPL-3.0 |

Changes relative to upstream are listed in [CHANGELOG.md](CHANGELOG.md).

### Referenced / vendored projects

| Project | Address | Licence | Used for |
|---|---|---|---|
| BluetoothLEExplorer (Microsoft) | https://github.com/microsoft/BluetoothLEExplorer | MIT | UWP GATT explorer framework, GATT services, collections |
| NXBT (Brikwerk) | https://github.com/Brikwerk/nxbt | MIT | Switch controller emulation (vendored in `NXBT/`) |
| joycontrol (mart1nro) | https://github.com/mart1nro/joycontrol | GPL-3.0 | Switch controller protocol (vendored in `JoyControl/`) |
| Nintendo_Switch_Reverse_Engineering (dekuNukem) | https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering | see upstream | Switch protocol documentation |

Build-time NuGet packages (Microsoft .NET Native / UWP) are listed in [NOTICE](NOTICE).

## Acknowledgements

The Bluetooth knowledge in this project comes almost entirely from the projects
listed above. Thanks to their authors.

---

## License

- This project's code: GPL-3.0
- JoyControl: GPL-3.0
- Code by Microsoft: MIT
- NXBT: MIT

See [LICENSE](LICENSE).
