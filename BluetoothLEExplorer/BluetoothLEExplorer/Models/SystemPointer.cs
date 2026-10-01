// <copyright file="SystemPointer.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
// Win32 cursor helpers for the mouse-capture page. UWP has no ClipCursor, so
// the page derives relative motion from pointer position deltas - which stop
// at the screen edge. Warping the system pointer back to the middle of its
// monitor keeps the deltas flowing.
//
// user32 is used rather than Windows.UI.Input.Preview.Injection on purpose:
// injected *relative* moves are scaled by the Windows mouse ballistics (a
// 60-unit pull can travel 140+ pixels), so a "pull to centre" overshoots the
// window, while SetCursorPos lands exactly where it is told. The coordinates
// here are whatever user32 speaks in this process (physical pixels for a
// DPI-aware UWP view) - the target is derived from GetCursorPos/MonitorFromPoint
// in the same space, so the units do not have to be known.
//----------------------------------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BluetoothLEExplorer.Models
{
    /// <summary>
    /// System pointer (OS cursor) helpers.
    /// </summary>
    public static class SystemPointer
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

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
        private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        /// <summary>MONITOR_DEFAULTTONEAREST.</summary>
        private const uint c_monitorDefaultToNearest = 2;

        // MONITORINFO: int + RECT + RECT + DWORD, no strings.
        private const int c_monitorInfoSize = 40;

        /// <summary>
        /// True when user32 cursor calls are usable from this process.
        /// </summary>
        /// <returns>True when the cursor position can be read.</returns>
        public static bool IsAvailable()
        {
            try
            {
                POINT p;
                return GetCursorPos(out p);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Moves the system pointer to the middle of the monitor it currently
        /// sits on. The capture page calls this when the pointer approaches a
        /// window edge; because capture runs fullscreen, the monitor centre is
        /// the middle of the window - and the warp is exact, so the position
        /// jump can be recognised and dropped before it reaches the phone.
        /// </summary>
        /// <returns>True when the pointer was moved.</returns>
        public static bool TryWarpToMonitorCenter()
        {
            try
            {
                POINT cursor;
                if (!GetCursorPos(out cursor))
                {
                    return false;
                }

                IntPtr monitor = MonitorFromPoint(cursor, c_monitorDefaultToNearest);
                if (monitor == IntPtr.Zero)
                {
                    return false;
                }

                var info = new MONITORINFO();
                info.cbSize = c_monitorInfoSize;
                if (!GetMonitorInfo(monitor, ref info))
                {
                    return false;
                }

                int centerX = info.rcMonitor.Left + ((info.rcMonitor.Right - info.rcMonitor.Left) / 2);
                int centerY = info.rcMonitor.Top + ((info.rcMonitor.Bottom - info.rcMonitor.Top) / 2);
                return SetCursorPos(centerX, centerY);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SystemPointer.TryWarpToMonitorCenter: " + ex.Message);
                return false;
            }
        }
    }
}
