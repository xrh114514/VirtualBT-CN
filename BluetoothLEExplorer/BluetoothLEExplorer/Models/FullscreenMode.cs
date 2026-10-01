// <copyright file="FullscreenMode.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
// How the mouse-capture page takes over the screen. Two presentations, picked
// in Settings and applied every time capture mode is entered:
//
//   Borderless  - ApplicationView.TryEnterFullScreenMode(). A borderless window
//                 filling the screen, DWM-composited. The system keeps a hidden
//                 title bar that the pointer summons from the top edge.
//   Exclusive   - a borderless topmost window covering the whole monitor, with
//                 the caption/sizing border stripped off the top-level window.
//                 Nothing to summon, no caption buttons to click by accident.
//                 This is as close to a game's exclusive fullscreen as a UWP
//                 XAML app gets - there is no DirectX swapchain to hand to
//                 DXGI SetFullscreenState.
//
// A UWP app's visible window is hosted by ApplicationFrameHost.exe (the
// top-level ApplicationFrameWindow belongs to another process; the app's own
// CoreWindow is a child of it), so "exclusive" has to style the frame window
// cross-process. That works - verified against the running app - but if it
// ever fails or the caption comes back, the caller falls back to Borderless.
//----------------------------------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Storage;

namespace BluetoothLEExplorer.Models
{
    /// <summary>
    /// Fullscreen presentation used by the mouse-capture page.
    /// </summary>
    public static class FullscreenMode
    {
        /// <summary>LocalSettings key holding the chosen presentation.</summary>
        public const string SettingKey = "MouseCaptureFullscreenMode";

        /// <summary>Value for the borderless-topmost window (game-style).</summary>
        public const string ExclusiveValue = "Exclusive";

        /// <summary>Value for ApplicationView fullscreen.</summary>
        public const string BorderlessValue = "Borderless";

        // ---- win32 ----
        private const int c_gwlStyle = -16;
        private const int c_gwlExStyle = -20;

        private const int c_wsCaption = 0x00C00000;
        private const int c_wsThickFrame = 0x00040000;
        private const int c_wsSysMenu = 0x00080000;
        private const int c_wsMinimizeBox = 0x00020000;
        private const int c_wsMaximizeBox = 0x00010000;

        private const int c_wsExDlgModalFrame = 0x00000001;
        private const int c_wsExWindowEdge = 0x00000100;

        private static readonly IntPtr s_hwndTopmost = new IntPtr(-1);
        private static readonly IntPtr s_hwndNotopmost = new IntPtr(-2);

        private const uint c_monitorDefaultToNearest = 2;
        private const int c_gaRoot = 2;

        private const uint c_swpNoSize = 0x0001;
        private const uint c_swpNoMove = 0x0002;
        private const uint c_swpNoZOrder = 0x0004;
        private const uint c_swpNoActivate = 0x0010;
        private const uint c_swpFrameChanged = 0x0020;
        private const uint c_swpShowWindow = 0x0040;

        private const int c_monitorInfoSize = 40;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr window, int index, int value);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr window, int flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        /// <summary>
        /// The interop interface that exposes a CoreWindow's HWND.
        /// </summary>
        [ComImport]
        [Guid("45D64A29-A63E-4CB6-B498-5781D298CB4F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICoreWindowInterop
        {
            IntPtr WindowHandle { get; }

            bool MessageHandled { set; }
        }

        // ---- saved frame state, restored when capture mode is left ----
        private static IntPtr s_frame;
        private static int s_savedStyle;
        private static int s_savedExStyle;
        private static RECT s_savedRect;
        private static bool s_active;

        /// <summary>
        /// True when the user picked the borderless-topmost presentation.
        /// </summary>
        /// <returns>True for exclusive.</returns>
        public static bool IsExclusive()
        {
            try
            {
                object v = ApplicationData.Current.LocalSettings.Values[SettingKey];
                return string.Equals(v as string, ExclusiveValue, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Saves the chosen presentation. The default is Borderless (the
        /// ApplicationView fullscreen behaviour previous versions used); the
        /// settings page lets the user switch to Exclusive.
        /// </summary>
        /// <param name="exclusive">True for the borderless-topmost window.</param>
        public static void Set(bool exclusive)
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values[SettingKey] =
                    exclusive ? ExclusiveValue : BorderlessValue;
            }
            catch (Exception)
            {
                // A failed save falls back to the default on the next launch.
            }
        }

        /// <summary>
        /// True when no explicit choice has been saved yet (used by the
        /// settings page to show the current default).
        /// </summary>
        /// <returns>True when unset.</returns>
        public static bool IsUnset()
        {
            try
            {
                return !ApplicationData.Current.LocalSettings.Values.ContainsKey(SettingKey);
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>
        /// Turns the top-level window into a borderless topmost window that
        /// covers the monitor it is on. The caption is removed so the pointer
        /// has nothing to summon at the top edge.
        /// </summary>
        /// <returns>True when the window is now borderless.</returns>
        public static bool TryEnterExclusive()
        {
            if (s_active)
            {
                return true;
            }

            IntPtr frame = TryGetFrameHandle();
            if (frame == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                s_savedStyle = GetWindowLong(frame, c_gwlStyle);
                s_savedExStyle = GetWindowLong(frame, c_gwlExStyle);
                if (!GetWindowRect(frame, out s_savedRect))
                {
                    return false;
                }

                int style = s_savedStyle & ~(c_wsCaption | c_wsThickFrame | c_wsSysMenu | c_wsMinimizeBox | c_wsMaximizeBox);
                SetWindowLong(frame, c_gwlStyle, style);
                SetWindowLong(frame, c_gwlExStyle, s_savedExStyle & ~(c_wsExDlgModalFrame | c_wsExWindowEdge));

                // Cover the whole monitor and stay above the taskbar.
                IntPtr monitor = MonitorFromWindow(frame, c_monitorDefaultToNearest);
                if (monitor == IntPtr.Zero)
                {
                    RestoreFrame(frame);
                    return false;
                }
                var info = new MONITORINFO();
                info.cbSize = c_monitorInfoSize;
                if (!GetMonitorInfo(monitor, ref info))
                {
                    RestoreFrame(frame);
                    return false;
                }
                RECT r = info.rcMonitor;
                if (!SetWindowPos(frame, s_hwndTopmost, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                                  c_swpFrameChanged | c_swpShowWindow))
                {
                    RestoreFrame(frame);
                    return false;
                }

                // The frame is owned by another process, which may refuse the
                // change or put the caption straight back. Read it back.
                int now = GetWindowLong(frame, c_gwlStyle);
                if ((now & c_wsCaption) != 0)
                {
                    RestoreFrame(frame);
                    return false;
                }

                s_frame = frame;
                s_active = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FullscreenMode.TryEnterExclusive: " + ex.Message);
                RestoreFrame(frame);
                return false;
            }
        }

        /// <summary>
        /// Puts the window back the way it was before
        /// <see cref="TryEnterExclusive"/>.
        /// </summary>
        public static void LeaveExclusive()
        {
            if (!s_active)
            {
                return;
            }

            try
            {
                RestoreFrame(s_frame);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FullscreenMode.LeaveExclusive: " + ex.Message);
            }
            s_active = false;
            s_frame = IntPtr.Zero;
        }

        private static void RestoreFrame(IntPtr frame)
        {
            if (frame == IntPtr.Zero)
            {
                return;
            }
            try
            {
                SetWindowLong(frame, c_gwlStyle, s_savedStyle);
                SetWindowLong(frame, c_gwlExStyle, s_savedExStyle);
                RECT r = s_savedRect;
                SetWindowPos(frame, s_hwndNotopmost, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                             c_swpFrameChanged | c_swpShowWindow);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("FullscreenMode.RestoreFrame: " + ex.Message);
            }
        }

        /// <summary>
        /// The top-level window that carries the caption - the app's own
        /// CoreWindow is a child of the ApplicationFrameHost frame.
        /// </summary>
        /// <returns>HWND, or zero when it cannot be found.</returns>
        private static IntPtr TryGetFrameHandle()
        {
            try
            {
                var coreWindow = Windows.UI.Xaml.Window.Current != null
                    ? Windows.UI.Xaml.Window.Current.CoreWindow
                    : null;
                if (coreWindow != null)
                {
                    // Cast via object: the compiler will not convert a projected
                    // WinRT class straight to a ComImport interface, but a runtime
                    // cast QueryInterfaces the RCW, which is what we want.
                    var interop = (ICoreWindowInterop)(object)coreWindow;
                    IntPtr hwnd = interop.WindowHandle;
                    if (hwnd != IntPtr.Zero)
                    {
                        IntPtr root = GetAncestor(hwnd, c_gaRoot);
                        return root != IntPtr.Zero ? root : hwnd;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ICoreWindowInterop unavailable: " + ex.Message);
            }

            // Fallback: while capture mode is entering, our frame is the
            // foreground top-level window.
            try
            {
                return GetForegroundWindow();
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
        }
    }
}
