# VirtualBT

**Virtual Bluetooth input device for Windows** — simulates the computer as a Bluetooth keyboard or mouse and forwards keyboard and mouse input to phones, tablets, and other devices.

The interface supports switching between Chinese and English.

> **中文**：[README.zh-CN.md](README.zh-CN.md)
>
> **Download**: prebuilt package on [GitHub Releases](https://github.com/xrh114514/VirtualBT-CN/releases/latest) — extract and double-click `启动VirtualBT.bat`.
>
> **Usage**: see [Quick-Start.md](Quick-Start.md); for detailed troubleshooting, see [Troubleshooting](#troubleshooting) below.
>
> **Upstream project**: [itsmikethetech/VirtualBT](https://github.com/itsmikethetech/VirtualBT) · [VirtualDrivers/VirtualBT](https://github.com/VirtualDrivers/VirtualBT)

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
| Bluetooth keyboard emulation | Forwards keystrokes from the computer to the connected device in real time (BLE HID) |
| Bluetooth mouse emulation | Relative motion, left/right/middle buttons, and wheel (BLE HID, 16-bit precision) |
| Full-screen game mode | Exclusive full screen with the pointer hidden; captures the mouse and keyboard at the same time, suited to camera control |
| BLE device scanning and pairing | Scans nearby Bluetooth Low Energy devices and pairs in-app |
| Chinese / English bilingual | Switchable on the Settings page; takes effect after the app restarts |
| GATT service debugging | Inspect and read/write characteristics, advertise beacons, monitor advertisements, and more (from BluetoothLEExplorer) |

---

## Quick start

### 1. Launch

Double-click **`启动VirtualBT.bat`**.

The script automatically performs the following steps:

- Checks for and installs any missing .NET Native 1.6 runtime;
- Registers the app package;
- Starts the app; if it is already running, brings its window to the foreground.

The window closes automatically on success; on failure it remains open and displays the error.

### 2. Use the computer as a Bluetooth keyboard or mouse

1. Make sure system Bluetooth is on (taskbar quick settings).
2. Open VirtualBT and go to **Virtual Keyboard** in the left sidebar.
3. Turn on the **“Discoverable and Connectable”** switch.
4. Search for and pair with the computer from your phone's Bluetooth settings.
5. Once connected, the device appears under “Connected clients”.

From then on, keystrokes are forwarded to the phone whenever the app window has focus.

### 3. Game mode (full-screen mouse and keyboard capture)

1. Make sure the phone is connected (a device appears in the client list).
2. Click **“Enter Mouse Capture (Fullscreen)”**.
3. In the black full-screen view, mouse movement controls the camera and keyboard input is used for movement or actions.

| Key | Action |
|---|---|
| **`Esc`** | Leave full screen (not forwarded to the phone) |
| **`F1`** | Show or hide the hint bar |
| **`Numpad +` / `-`** | Adjust sensitivity, range 0.1x to 5.0x |
| Any other key | All forwarded to the phone (including `Tab`, `WASD`, `Shift`, etc.) |

### 4. Switch language

**Settings → Language → 中文 / English**. The app restarts automatically; the change takes effect after the restart.

---

## System requirements

- Windows 10 version 1903+ (build 18362+); verified on Windows 11.
- A Bluetooth adapter that supports the **BLE peripheral (Peripheral) role**.
  - Common Intel Wireless Bluetooth (AX series) adapters need a recent driver.
  - If unsure, try it first; if you hit `RadioNotAvailable`, see the troubleshooting below.
- Developer mode or sideloading allowed (needed when installing the app package).

---

## Troubleshooting

### `无法创建HID服务提供程序：RadioNotAvailable`

The Bluetooth advertising resource is unavailable, so neither the keyboard nor the mouse can send anything. The app retries 6 times automatically and prints a diagnostic. Try the following in order:

1. Make sure system Bluetooth is on (by far the most common cause).
2. Close Phone Link and any other programs that use Bluetooth.
3. Turn Bluetooth off, wait 5 seconds, and turn it back on.
4. In Device Manager, locate Bluetooth → `Intel(R) Wireless Bluetooth(R)`, right-click “Disable”, then “Enable”.
5. Update the Intel Wireless Bluetooth driver.
6. Restart the computer.

If none of the above helps, the adapter does not support the peripheral role. Replace it with a USB Bluetooth adapter that supports BLE Peripheral (such as a CSR8510 or a recent Realtek BLE 5.x).

### The phone cannot find the computer

- The switch on the computer side is not on (“Discoverable and Connectable”).
- Trigger “Search for devices” manually in the phone's Bluetooth settings; do not rely on the quick panel alone.
- Some phone models need Bluetooth turned off and on again before they can find HID devices.

### Clicking “Pair” on the “Discover and Pair” page says there is no permission

This is normal. Phones are generally not connected by the computer as Bluetooth peripherals. To use the computer as a keyboard for a phone, use the “Virtual Keyboard” page and pair from the phone side — the connection direction is reversed.

### The mouse stops at the screen edge

UWP has no mouse-capture API. When the pointer reaches the screen boundary it stops reporting relative motion. Single-monitor full-screen use is usually sufficient.

### The language switch has no effect

The Settings page saves the choice and restarts the app automatically. If it still has no effect, check the app's language permissions in Windows Settings.

---

## Repository layout

```
VirtualBT-main/
├── 启动VirtualBT.bat          # Launch entry point (recommended)
├── launch-virtualbt.ps1       # Launcher logic (deps, register, start, bring to front)
├── deploy/
│   ├── AppX/                  # Deployed app package (build output)
│   ├── nuget/                 # .NET Native 1.6 runtime appx
│   └── refasm/                # .NETCore v5.0 reference assemblies needed to compile
├── BluetoothLEExplorer/       # Main project (UWP)
│   ├── VirtualBT.sln
│   └── BluetoothLEExplorer/
│       ├── Models/            # VirtualKeyboard / SafeDispatcher / Loc …
│       ├── Views/             # Pages (Discover / VirtualKeyboard / MouseCapture …)
│       ├── ViewModels/
│       └── Resources/         # zh-CN / en-US string tables
├── GattServicesLibrary/       # GATT service library
├── SortedObservableCollection/
├── NXBT/                      # Switch controller emulation (Linux only)
└── JoyControl/                # Switch controller emulation (Linux only)
```

---

## Building from source

> Ordinary use does not require building; just run `启动VirtualBT.bat`.

### Toolchain

**Visual Studio Community 2022** is required, with the following selected:

- `Universal Windows Platform development` (`Microsoft.VisualStudio.Workload.Universal`)
- Windows 10 SDK 10.0.19041
- .NET Native compiler

The complete install command verified to work:

```powershell
vs_community.exe --add Microsoft.VisualStudio.Workload.Universal `
  --add Microsoft.VisualStudio.Component.Windows10SDK.19041 `
  --add Microsoft.VisualStudio.Component.DotNetNative --quiet --norestart --wait
```

> Visual Studio Build Tools 2022 **does not include** the UWP workload; even with it installed this project cannot be compiled.

### Compile

```bash
MSBuild.exe BluetoothLEExplorer/BluetoothLEExplorer/VirtualBT.csproj \
  -t:Build -p:Configuration=Release -p:Platform=x86 \
  -p:UseDotNetNativeToolchain=true \
  -p:TargetFrameworkRootPath=D:/VirtualBT-main/deploy/refasm
```

`TargetFrameworkRootPath` points at `deploy/refasm/`, because the `.NETCore v5.0` reference assemblies are not installed with Visual Studio and have to be collected from the NuGet `ref/netcore50` folders.

### Deploy

```bash
# Output lands in bin/x86/Release/ilc/
rm -rf deploy/AppX
cp -r BluetoothLEExplorer/BluetoothLEExplorer/bin/x86/Release/ilc deploy/AppX

# Re-register
powershell -Command "Get-AppxPackage -Name 'Microsoft.BluetoothLEExplorer' | Remove-AppxPackage; \
  Add-AppxPackage -Register 'D:\VirtualBT-main\deploy\AppX\AppxManifest.xml'"
```

If the dependency packages in `deploy/nuget/` are not installed yet:

```powershell
Add-AppxPackage -Path deploy\nuget\Microsoft.NET.Native.Runtime.1.6.appx
Add-AppxPackage -Path deploy\nuget\Microsoft.NET.Native.Framework.1.6.appx
```

---

## Known limitations

- **Mouse capture** derives relative motion from position deltas; it stops when the pointer reaches the screen edge (see troubleshooting).
- **Keystrokes are only forwarded while the app window has focus** (game mode is exclusive full screen, so this limitation does not apply there).
- **Switch controller emulation** (`NXBT` / `JoyControl`) depends on Linux BlueZ and is not available on Windows.
- **Gamepad** HID reports are not implemented yet (keyboard and mouse only).

---

## Developer docs

Implementation details and problem notes are in **[docs/开发笔记.md](docs/开发笔记.md)** ([English](docs/developer-notes.md)):

- Root cause and fix for the Win11 24H2+ Bluetooth scan crash
- Three pitfalls of localization (Chinese/English switching)
- HID keyboard and mouse report formats
- Build environment details

## Contributing ideas

Possible directions:

- Use `InputInjector` to recentre at the screen center for “infinite” mouse movement
- HID gamepad and touchpad reports
- Mouse DPI / acceleration curve settings
- Start with Windows, stay in the system tray

---

## Upstream project & referenced projects

This repository is a **modified version of the upstream VirtualBT project**. Full licence attribution is in [NOTICE](NOTICE).

### Upstream / origin

| Project | Address | Licence |
|---|---|---|
| **VirtualBT** (this project's origin) | https://github.com/itsmikethetech/VirtualBT | GPL-3.0 |
| VirtualBT (organisation mirror) | https://github.com/VirtualDrivers/VirtualBT | GPL-3.0 |

Changes relative to upstream are listed in [CHANGELOG.md](CHANGELOG.md).

### Referenced / vendored projects

| Project | Address | Licence | Used for |
|---|---|---|---|
| BluetoothLEExplorer (Microsoft) | https://github.com/microsoft/BluetoothLEExplorer | MIT | UWP GATT debugging framework, GATT service library, collection controls |
| NXBT (Brikwerk) | https://github.com/Brikwerk/nxbt | MIT | Switch controller emulation (vendored in `NXBT/`) |
| joycontrol (mart1nro) | https://github.com/mart1nro/joycontrol | GPL-3.0 | Switch controller protocol (vendored in `JoyControl/`) |
| Nintendo_Switch_Reverse_Engineering (dekuNukem) | https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering | see upstream | Switch protocol reverse-engineering documentation |

Build-time NuGet packages (Microsoft .NET Native / UWP) are listed in [NOTICE](NOTICE).

## Acknowledgements

The Bluetooth-related knowledge in this project comes mainly from the projects listed above; the authors have my thanks.

- [microsoft/BluetoothLEExplorer](https://github.com/microsoft/BluetoothLEExplorer) — UWP GATT debugging framework
- [mart1nro/joycontrol](https://github.com/mart1nro/joycontrol) — Switch controller protocol
- [Brikwerk/nxbt](https://github.com/Brikwerk/nxbt) — Switch controller emulation
- [dekuNukem/Nintendo_Switch_Reverse_Engineering](https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering) — Switch protocol reverse-engineering documentation

---

## License

- This project's code: GPL-3.0
- JoyControl: GPL-3.0
- Code by Microsoft: MIT
- NXBT: MIT

See [LICENSE](LICENSE).
