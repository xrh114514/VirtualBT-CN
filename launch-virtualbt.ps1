# VirtualBT one-click launcher
# Idempotent: installs missing framework packages, registers the app if
# needed, then launches (or focuses) the window.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$appx = Join-Path $root 'deploy\AppX'
$nuget = Join-Path $root 'deploy\nuget'
$aumid = 'Microsoft.BluetoothLEExplorer_8wekyb3d8bbwe!App'
$pkgName = 'Microsoft.BluetoothLEExplorer'

function Focus-Window {
    $sig = @'
using System;
using System.Runtime.InteropServices;
public class Win32Focus {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int n);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
}
'@
    Add-Type -TypeDefinition $sig -ErrorAction SilentlyContinue
    $host1 = Get-Process -Name 'ApplicationFrameHost' -ErrorAction SilentlyContinue |
             Where-Object { $_.MainWindowTitle -eq 'VirtualBT' } | Select-Object -First 1
    if (-not $host1) {
        $host1 = Get-Process -Name 'VirtualBT' -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    if ($host1 -and $host1.MainWindowHandle -ne 0) {
        if ([Win32Focus]::IsIconic($host1.MainWindowHandle)) {
            [Win32Focus]::ShowWindow($host1.MainWindowHandle, 9) | Out-Null
        }
        [Win32Focus]::SetForegroundWindow($host1.MainWindowHandle) | Out-Null
        return $true
    }
    return $false
}

# --- 1. already running? focus and exit ---
$running = Get-Process -Name 'VirtualBT' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host '[1/4] VirtualBT is already running - focusing window...'
    Start-Sleep -Milliseconds 300
    if (Focus-Window) {
        Write-Host 'OK. (If you cannot see it, check the taskbar.)'
        exit 0
    }
    Write-Host 'Process found but no window yet; continuing to launch...'
}

# --- 2. framework packages ---
Write-Host '[2/4] Checking .NET Native 1.6 runtime packages...'
$need = @(
    @{ Name = 'Microsoft.NET.Native.Runtime.1.6';  File = 'Microsoft.NET.Native.Runtime.1.6.appx' },
    @{ Name = 'Microsoft.NET.Native.Framework.1.6'; File = 'Microsoft.NET.Native.Framework.1.6.appx' }
)
foreach ($p in $need) {
    $installed = Get-AppxPackage -Name $p.Name -ErrorAction SilentlyContinue
    if (-not $installed) {
        $path = Join-Path $nuget $p.File
        if (-not (Test-Path $path)) {
            Write-Host ("  MISSING: " + $p.Name + " and installer not found at " + $path)
            Write-Host '  Cannot continue. Re-copy the deploy folder.'
            exit 1
        }
        Write-Host ("  Installing " + $p.Name + " ...")
        Add-AppxPackage -Path $path | Out-Null
        Write-Host '  OK'
    } else {
        Write-Host ("  " + $p.Name + " already installed")
    }
}

# --- 3. register app ---
Write-Host '[3/4] Checking app registration...'
$pkg = Get-AppxPackage -Name $pkgName -ErrorAction SilentlyContinue
if (-not $pkg) {
    $manifest = Join-Path $appx 'AppxManifest.xml'
    if (-not (Test-Path $manifest)) {
        Write-Host ("  MISSING: " + $manifest)
        exit 1
    }
    Write-Host '  Registering VirtualBT...'
    Add-AppxPackage -Register $manifest
    Write-Host '  OK'
} else {
    Write-Host ("  Already registered: " + $pkg.PackageFullName + " (" + $pkg.Status + ")")
}

# --- 4. launch ---
Write-Host '[4/4] Launching VirtualBT...'
Start-Process explorer.exe -ArgumentList ("shell:AppsFolder\" + $aumid)

$deadline = (Get-Date).AddSeconds(15)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
    if (Get-Process -Name 'VirtualBT' -ErrorAction SilentlyContinue) {
        Start-Sleep -Seconds 2
        Focus-Window | Out-Null
        Write-Host 'OK - VirtualBT is running.'
        exit 0
    }
}
Write-Host 'WARNING: process did not appear within 15 s.'
Write-Host 'Try launching "VirtualBT" from the Start menu.'
exit 1
