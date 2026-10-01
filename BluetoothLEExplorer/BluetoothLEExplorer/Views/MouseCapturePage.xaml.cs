// <copyright file="MouseCapturePage.xaml.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
// Fullscreen "game mode" page: captures relative mouse movement, buttons and
// wheel and streams them to the phone as BLE HID mouse reports.
//
// Relative movement is derived from CoreWindow.PointerMoved (position deltas)
// because Windows.UI.Input.MouseDevice is not resolvable in this projection.
// Accumulated deltas are flushed every c_flushIntervalMs so we stay inside a
// typical BLE connection interval instead of flooding the ATT queue.
//----------------------------------------------------------------------------------------------
using System;
using System.Diagnostics;
using System.Threading;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BluetoothLEExplorer.Views
{
    /// <summary>
    /// Fullscreen mouse capture page.
    /// </summary>
    public sealed partial class MouseCapturePage : Page
    {
        /// <summary>How often we flush accumulated mouse deltas (ms).</summary>
        private const int c_flushIntervalMs = 8;

        private Models.VirtualKeyboard m_keyboard;

        // ---- accumulated report state (guarded by m_stateLock) ----
        private readonly object m_stateLock = new object();
        private int m_accX;
        private int m_accY;
        private int m_accWheel;
        private byte m_buttons;
        private bool m_pendingButtons;

        // Pointer position tracking used to derive relative movement.
        private bool m_haveLastPos;
        private double m_lastX;
        private double m_lastY;

        private Timer m_flushTimer;
        private bool m_hooked;
        private bool m_wasFullscreen;
        private int m_reportsSent;
        private double m_sensitivity = 1.0;

        /// <summary>
        /// Initializes a new instance of the <see cref="MouseCapturePage" /> class.
        /// </summary>
        public MouseCapturePage()
        {
            this.InitializeComponent();
            m_sensitivity = 1.0;
        }

        /// <inheritdoc />
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            m_keyboard = Models.VirtualKeyboard.Current;
            if (m_keyboard == null || !m_keyboard.HasSubscribedClients)
            {
                WarnText.Text = "尚未有设备连接。请先在「虚拟蓝牙键盘」页打开广播，并在手机上配对连接。";
                return;
            }

            EnterCaptureMode();
        }

        /// <inheritdoc />
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            LeaveCaptureMode();
            base.OnNavigatedFrom(e);
        }

        /// <summary>
        /// Turns on fullscreen + hidden cursor and starts streaming reports.
        /// </summary>
        private void EnterCaptureMode()
        {
            var view = ApplicationView.GetForCurrentView();
            m_wasFullscreen = view.IsFullScreenMode;
            if (!m_wasFullscreen)
            {
                view.TryEnterFullScreenMode();
            }

            // Hide the cursor for the game-like feel.
            Window.Current.CoreWindow.PointerCursor = null;

            var coreWindow = Window.Current.CoreWindow;
            coreWindow.PointerMoved += OnPointerMoved;
            coreWindow.PointerPressed += OnPointerPressed;
            coreWindow.PointerReleased += OnPointerReleased;
            coreWindow.PointerWheelChanged += OnPointerWheelChanged;
            coreWindow.KeyDown += OnKeyDown;
            coreWindow.KeyUp += OnKeyUp;

            m_hooked = true;
            m_haveLastPos = false;

            // This page now forwards BOTH mouse and keyboard. Tell the keyboard
            // page's CoreWindow handlers to stay silent so nothing is duplicated.
            Models.VirtualKeyboard.CapturePageOwnsKeyboard = true;

            m_flushTimer = new Timer(FlushAccumulated, null, c_flushIntervalMs, c_flushIntervalMs);

            StatusText.Text = "已进入捕获模式 - 敏感度 " + m_sensitivity.ToString("0.0") + "x - 已发送 0 条报告";
        }

        /// <summary>
        /// Restores the normal window / cursor state and stops streaming.
        /// </summary>
        private void LeaveCaptureMode()
        {
            m_hooked = false;
            Models.VirtualKeyboard.CapturePageOwnsKeyboard = false;

            if (m_flushTimer != null)
            {
                m_flushTimer.Dispose();
                m_flushTimer = null;
            }

            if (Window.Current != null && Window.Current.CoreWindow != null)
            {
                var coreWindow = Window.Current.CoreWindow;
                coreWindow.PointerMoved -= OnPointerMoved;
                coreWindow.PointerPressed -= OnPointerPressed;
                coreWindow.PointerReleased -= OnPointerReleased;
                coreWindow.PointerWheelChanged -= OnPointerWheelChanged;
                coreWindow.KeyDown -= OnKeyDown;
                coreWindow.KeyUp -= OnKeyUp;

                coreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
            }

            if (!m_wasFullscreen)
            {
                ApplicationView.GetForCurrentView().ExitFullScreenMode();
            }

            // Send a final "all released" report so the phone is not left with
            // a stuck button / endless drift.
            try
            {
                if (Models.VirtualKeyboard.Current != null)
                {
                    Models.VirtualKeyboard.Current.SendMouseReport(0, 0, 0, 0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to send final mouse report: " + ex.Message);
            }
        }

        /// <summary>
        /// Derives relative movement from consecutive pointer positions.
        /// </summary>
        /// <param name="sender">Core window.</param>
        /// <param name="args">Pointer args.</param>
        private void OnPointerMoved(CoreWindow sender, PointerEventArgs args)
        {
            if (!m_hooked)
            {
                return;
            }

            var pos = args.CurrentPoint.Position;
            if (!m_haveLastPos)
            {
                m_lastX = pos.X;
                m_lastY = pos.Y;
                m_haveLastPos = true;
                return;
            }

            double dx = (pos.X - m_lastX) * m_sensitivity;
            double dy = (pos.Y - m_lastY) * m_sensitivity;
            m_lastX = pos.X;
            m_lastY = pos.Y;

            if (dx == 0 && dy == 0)
            {
                return;
            }

            lock (m_stateLock)
            {
                m_accX += (int)Math.Round(dx);
                m_accY += (int)Math.Round(dy);
            }
        }

        /// <summary>
        /// Mouse button down.
        /// </summary>
        /// <param name="sender">Core window.</param>
        /// <param name="args">Pointer args.</param>
        private void OnPointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            if (!m_hooked)
            {
                return;
            }

            TrackButtons(args);
        }

        /// <summary>
        /// Mouse button up.
        /// </summary>
        /// <param name="sender">Core window.</param>
        /// <param name="args">Pointer args.</param>
        private void OnPointerReleased(CoreWindow sender, PointerEventArgs args)
        {
            if (!m_hooked)
            {
                return;
            }

            TrackButtons(args);
        }

        /// <summary>
        /// Mouse wheel.
        /// </summary>
        /// <param name="sender">Core window.</param>
        /// <param name="args">Pointer args.</param>
        private void OnPointerWheelChanged(CoreWindow sender, PointerEventArgs args)
        {
            if (!m_hooked)
            {
                return;
            }

            int delta = args.CurrentPoint.Properties.MouseWheelDelta;
            int notches = delta / 120;
            if (notches == 0)
            {
                notches = delta > 0 ? 1 : (delta < 0 ? -1 : 0);
            }

            lock (m_stateLock)
            {
                m_accWheel += notches;
            }
        }

        /// <summary>
        /// Keyboard shortcuts for the capture page.
        /// </summary>
        /// <param name="sender">Core window.</param>
        /// <param name="args">Key args.</param>
        private void OnKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            // Esc is reserved to leave capture mode and is NOT forwarded to the
            // phone, so a game cannot trap the user in fullscreen.
            if (args.VirtualKey == VirtualKey.Escape)
            {
                if (Frame != null && Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
                return;
            }

            // F1 toggles the help overlay (Tab is forwarded to the game instead).
            if (args.VirtualKey == VirtualKey.F1)
            {
                Overlay.Visibility = Overlay.Visibility == Windows.UI.Xaml.Visibility.Visible
                    ? Windows.UI.Xaml.Visibility.Collapsed
                    : Windows.UI.Xaml.Visibility.Visible;
            }

            // Numpad +/- adjust sensitivity without touching the phone.
            if (args.VirtualKey == VirtualKey.Add)
            {
                m_sensitivity = Math.Min(5.0, m_sensitivity + 0.1);
                return;
            }
            if (args.VirtualKey == VirtualKey.Subtract)
            {
                m_sensitivity = Math.Max(0.1, m_sensitivity - 0.1);
                return;
            }

            // Everything else - including Tab - goes to the phone as a key press.
            var kb = Models.VirtualKeyboard.Current;
            if (kb != null && !args.KeyStatus.WasKeyDown)
            {
                kb.PressKey(Models.HidHelper.GetPs2Set1ScanCodeFromStatus(args.KeyStatus));
            }
        }

        /// <summary>
        /// Forwards key releases to the phone.
        /// </summary>
        /// <param name="sender">Core window.</param>
        /// <param name="args">Key args.</param>
        private void OnKeyUp(CoreWindow sender, KeyEventArgs args)
        {
            // Swallow the Esc release so it never reaches the phone.
            if (args.VirtualKey == VirtualKey.Escape)
            {
                return;
            }

            if (args.VirtualKey == VirtualKey.F1 ||
                args.VirtualKey == VirtualKey.Add ||
                args.VirtualKey == VirtualKey.Subtract)
            {
                return;
            }

            var kb = Models.VirtualKeyboard.Current;
            if (kb != null && args.KeyStatus.IsKeyReleased)
            {
                kb.ReleaseKey(Models.HidHelper.GetPs2Set1ScanCodeFromStatus(args.KeyStatus));
            }
        }

        /// <summary>
        /// Updates the tracked button state from a pointer event.
        /// </summary>
        /// <param name="args">Pointer args.</param>
        private void TrackButtons(PointerEventArgs args)
        {
            byte buttons = ReadButtons(args);
            lock (m_stateLock)
            {
                if (buttons != m_buttons)
                {
                    m_buttons = buttons;
                    m_pendingButtons = true;
                }
            }
        }

        /// <summary>
        /// Reads the current button state from a pointer event.
        /// </summary>
        /// <param name="args">Pointer args.</param>
        /// <returns>Bitfield: 0x01=left, 0x02=right, 0x04=middle.</returns>
        private static byte ReadButtons(PointerEventArgs args)
        {
            var props = args.CurrentPoint.Properties;
            byte buttons = 0;
            if (props.IsLeftButtonPressed)
            {
                buttons |= 0x01;
            }
            if (props.IsRightButtonPressed)
            {
                buttons |= 0x02;
            }
            if (props.IsMiddleButtonPressed)
            {
                buttons |= 0x04;
            }
            return buttons;
        }

        /// <summary>
        /// Timer callback: send whatever movement / button change has piled up.
        /// </summary>
        /// <param name="state">Unused.</param>
        private void FlushAccumulated(object state)
        {
            if (!m_hooked)
            {
                return;
            }

            int x, y, wheel;
            byte buttons;
            bool buttonsChanged;

            lock (m_stateLock)
            {
                x = ClampToShort(m_accX);
                y = ClampToShort(m_accY);
                wheel = ClampToSByte(m_accWheel);
                buttons = m_buttons;
                buttonsChanged = m_pendingButtons;

                m_accX -= x;
                m_accY -= y;
                m_accWheel -= wheel;
                m_pendingButtons = false;
            }

            if (x == 0 && y == 0 && wheel == 0 && !buttonsChanged)
            {
                return;
            }

            var kb = m_keyboard;
            if (kb == null)
            {
                return;
            }

            kb.SendMouseReport(buttons, (short)x, (short)y, (sbyte)wheel);
            Interlocked.Increment(ref m_reportsSent);
        }

        /// <summary>
        /// Clamps to Int16 range.
        /// </summary>
        /// <param name="v">Value.</param>
        /// <returns>Clamped value.</returns>
        private static int ClampToShort(int v)
        {
            if (v > short.MaxValue)
            {
                return short.MaxValue;
            }
            if (v < short.MinValue)
            {
                return short.MinValue;
            }
            return v;
        }

        /// <summary>
        /// Clamps to SByte range.
        /// </summary>
        /// <param name="v">Value.</param>
        /// <returns>Clamped value.</returns>
        private static int ClampToSByte(int v)
        {
            if (v > sbyte.MaxValue)
            {
                return sbyte.MaxValue;
            }
            if (v < sbyte.MinValue)
            {
                return sbyte.MinValue;
            }
            return v;
        }
    }
}
