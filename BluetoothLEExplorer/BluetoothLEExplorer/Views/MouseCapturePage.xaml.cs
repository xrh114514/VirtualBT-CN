// <copyright file="MouseCapturePage.xaml.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
// Fullscreen "game mode" page: captures relative mouse movement, buttons and
// wheel and streams them to the phone as BLE HID mouse reports. Keyboard
// input is forwarded too, and while this page is active NOTHING reaches the
// PC's own window - no shortcuts, no Tab focus moves, no text input - so a
// game cannot trigger desktop UI by accident. The configurable exit hotkey
// (Models.CaptureHotkey, default Esc) is the only key kept local.
//
// Relative movement is derived from CoreWindow.PointerMoved (position deltas)
// because Windows.UI.Input.MouseDevice is not resolvable in this projection.
// Those deltas stop at the window/screen edge and a pointer at the top edge
// summons the fullscreen title-bar overlay, so whenever the pointer comes
// near an edge it is pulled back to the middle of the window with
// Windows.UI.Input.Preview.Injection (see docs/developer-notes.md).
//
// Accumulated deltas are flushed every c_flushIntervalMs so we stay inside a
// typical BLE connection interval instead of flooding the ATT queue.
//----------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input.Preview.Injection;
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

        /// <summary>Distance to a window edge (DIPs) that pulls the pointer back.</summary>
        private const double c_edgeMarginDip = 48.0;

        /// <summary>Jump (DIPs) treated as the recenter teleport instead of user movement.</summary>
        private const double c_jumpThresholdDip = 120.0;

        /// <summary>How long to wait for the teleport to land before resyncing (ms).</summary>
        private const int c_recenterTimeoutMs = 400;

        /// <summary>Middle of the primary display in InputInjector's 0-65535 space.</summary>
        private const int c_normalizedCenter = 32768;

        /// <summary>FullScreenSystemOverlayMode.Hidden, which the 19041 SDK does not name.</summary>
        private const int c_overlayModeHidden = 2;

        private Models.VirtualKeyboard m_keyboard;
        private Models.CaptureHotkey m_exitHotkey;

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

        // Recenter bookkeeping. Written and read on the UI thread only.
        private InputInjector m_injector;
        private bool m_recenterPending;
        private int m_recenterPendingSince;

        // Scan codes whose press we forwarded, so the matching release is the
        // only thing that is sent (a press that was kept local - exit hotkey,
        // F1, ... - must not be released on the phone either).
        private readonly HashSet<uint> m_forwardedPresses = new HashSet<uint>();

        private Timer m_flushTimer;
        private bool m_entered;
        private bool m_hooked;
        private bool m_exiting;
        private bool m_exclusiveActive;
        private bool m_wasFullscreen;
        private FullScreenSystemOverlayMode m_previousOverlayMode;

        // Shell navigation chrome, hidden while capturing and put back on leave.
        private bool m_chromeHidden;
        private Windows.UI.Xaml.Visibility m_savedHamburgerVisibility;
        private SplitViewDisplayMode m_savedDisplayMode;
        private SplitViewDisplayMode m_savedNarrowDisplayMode;
        private SplitViewDisplayMode m_savedNormalDisplayMode;
        private SplitViewDisplayMode m_savedWideDisplayMode;
        private bool m_savedPaneOpen;
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

            m_exitHotkey = Models.CaptureHotkey.Load();
            HintText.Text = Models.Loc.Format(
                "Str_Mouse_keyboard_are_captured_and_forw.Text",
                m_exitHotkey.Display());

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
            m_entered = true;

            var view = ApplicationView.GetForCurrentView();
            m_wasFullscreen = view.IsFullScreenMode;
            m_previousOverlayMode = view.FullScreenSystemOverlayMode;

            // Two presentations (Settings -> 游戏模式全屏):
            //  - Exclusive:  borderless topmost window covering the monitor,
            //    caption stripped, so the pointer has nothing to summon.
            //  - Borderless: ApplicationView fullscreen; the system may slide a
            //    title bar down when the pointer reaches the top edge.
            // Exclusive falls back to Borderless when the frame cannot be
            // styled, so capture mode always takes the screen one way or the other.
            m_exclusiveActive = false;
            if (Models.FullscreenMode.IsExclusive() && Models.FullscreenMode.TryEnterExclusive())
            {
                m_exclusiveActive = true;
            }
            else
            {
                // Keep edge swipes / a pointer at the top edge from summoning
                // the title bar or taskbar over the game. Hidden is best when
                // the OS knows it; Minimal is the most the 19041 projection
                // names. The overlay mode has to be set before entering
                // fullscreen - the parameterless TryEnterFullScreenMode
                // overload is all this SDK has.
                ApplyOverlayMode(view);
                if (!m_wasFullscreen)
                {
                    view.TryEnterFullScreenMode();
                }
            }

            // Hide the navigation chrome. The sidebar would otherwise sit over
            // the left of the window: the capture area does not fill the screen
            // and a camera click landing on a nav button navigates away from
            // the page mid-capture.
            HideShellChrome();

            // Hide the cursor for the game-like feel.
            Window.Current.CoreWindow.PointerCursor = null;

            var coreWindow = Window.Current.CoreWindow;
            coreWindow.PointerMoved += OnPointerMoved;
            coreWindow.PointerPressed += OnPointerPressed;
            coreWindow.PointerReleased += OnPointerReleased;
            coreWindow.PointerWheelChanged += OnPointerWheelChanged;
            coreWindow.KeyDown += OnKeyDown;
            coreWindow.KeyUp += OnKeyUp;
            coreWindow.CharacterReceived += OnCharacterReceived;

            // The dispatcher-level hook is what actually keeps XAML from seeing
            // the keys (accelerators, access keys, Tab focus moves, ...).
            // CoreWindow.KeyDown alone does not.
            coreWindow.Dispatcher.AcceleratorKeyActivated += OnAcceleratorKeyActivated;

            try
            {
                m_injector = InputInjector.TryCreate();
            }
            catch (Exception ex)
            {
                m_injector = null;
                Debug.WriteLine("InputInjector unavailable: " + ex.Message);
            }

            // Which mechanism will warp the pointer back from the screen edge.
            // Shown in the status line so a broken warp is obvious instead of
            // looking like "the mouse stops at the edge again".
            string warpMode;
            if (Models.SystemPointer.IsAvailable())
            {
                warpMode = "win32";
            }
            else if (m_injector != null)
            {
                warpMode = "injector";
            }
            else
            {
                warpMode = "none";
                WarnText.Text = Models.Loc.Get("Str_MouseCaptureNoInjector.Text");
            }

            m_hooked = true;
            m_exiting = false;
            m_haveLastPos = false;
            m_recenterPending = false;
            m_forwardedPresses.Clear();

            // This page now forwards BOTH mouse and keyboard. Tell the keyboard
            // page's CoreWindow handlers to stay silent so nothing is duplicated.
            Models.VirtualKeyboard.CapturePageOwnsKeyboard = true;

            m_flushTimer = new Timer(FlushAccumulated, null, c_flushIntervalMs, c_flushIntervalMs);

            StatusText.Text = "已进入捕获模式 - " + (m_exclusiveActive ? "独占全屏" : "无边框全屏") +
                              " - 敏感度 " + m_sensitivity.ToString("0.0") + "x - 回中 " + warpMode +
                              " - 已发送 0 条报告";
        }

        /// <summary>
        /// Restores the normal window / cursor state and stops streaming.
        /// </summary>
        private void LeaveCaptureMode()
        {
            if (!m_entered)
            {
                return;
            }

            m_hooked = false;
            m_entered = false;
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
                coreWindow.CharacterReceived -= OnCharacterReceived;
                coreWindow.Dispatcher.AcceleratorKeyActivated -= OnAcceleratorKeyActivated;

                coreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
            }

            m_injector = null;
            m_recenterPending = false;

            RestoreShellChrome();

            var view = ApplicationView.GetForCurrentView();
            if (m_exclusiveActive)
            {
                // Borderless-topmost window: put the frame back exactly as it
                // was. ApplicationView fullscreen was never entered.
                Models.FullscreenMode.LeaveExclusive();
                m_exclusiveActive = false;
            }
            else if (!m_wasFullscreen)
            {
                view.ExitFullScreenMode();
            }
            try
            {
                view.FullScreenSystemOverlayMode = m_previousOverlayMode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to restore the overlay mode: " + ex.Message);
            }

            // Release anything still held (the exit chord's modifier, a key the
            // game still thinks is down) and drop the mouse buttons so the phone
            // is not left with a stuck key / endless drift.
            try
            {
                if (Models.VirtualKeyboard.Current != null)
                {
                    Models.VirtualKeyboard.Current.ReleaseAllKeys();
                    Models.VirtualKeyboard.Current.SendMouseReport(0, 0, 0, 0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to send final reports: " + ex.Message);
            }
        }

        /// <summary>
        /// Sets the fullscreen chrome policy, preferring Hidden over Minimal.
        /// </summary>
        /// <param name="view">Current application view.</param>
        private static void ApplyOverlayMode(ApplicationView view)
        {
            // Minimal is the most the 19041 projection names and already stops
            // the full title bar from sliding down over the game. Set it first
            // so an unknown value can never leave us on Standard.
            view.FullScreenSystemOverlayMode = FullScreenSystemOverlayMode.Minimal;

            // Hidden (value 2 on Windows builds that have it) also removes the
            // small edge strip. Read it back: an OS that does not know the
            // value would otherwise sit on whatever it last had.
            try
            {
                view.FullScreenSystemOverlayMode = (FullScreenSystemOverlayMode)c_overlayModeHidden;
            }
            catch (Exception)
            {
            }
            if ((int)view.FullScreenSystemOverlayMode != c_overlayModeHidden)
            {
                view.FullScreenSystemOverlayMode = FullScreenSystemOverlayMode.Minimal;
            }
        }

        /// <summary>
        /// Collapses the shell's navigation pane and hamburger button so the
        /// capture page fills the window and no nav button can swallow a
        /// camera click. The pane is closed and every visual-state display
        /// mode is set to Overlay, so a width change cannot bring it back.
        /// </summary>
        private void HideShellChrome()
        {
            try
            {
                if (Shell.Instance == null)
                {
                    return;
                }
                var menu = Shell.HamburgerMenu;
                if (menu == null)
                {
                    return;
                }

                m_savedHamburgerVisibility = menu.HamburgerButtonVisibility;
                m_savedDisplayMode = menu.DisplayMode;
                m_savedNarrowDisplayMode = menu.VisualStateNarrowDisplayMode;
                m_savedNormalDisplayMode = menu.VisualStateNormalDisplayMode;
                m_savedWideDisplayMode = menu.VisualStateWideDisplayMode;
                m_savedPaneOpen = menu.IsOpen;

                menu.HamburgerButtonVisibility = Windows.UI.Xaml.Visibility.Collapsed;
                menu.DisplayMode = SplitViewDisplayMode.Overlay;
                menu.VisualStateNarrowDisplayMode = SplitViewDisplayMode.Overlay;
                menu.VisualStateNormalDisplayMode = SplitViewDisplayMode.Overlay;
                menu.VisualStateWideDisplayMode = SplitViewDisplayMode.Overlay;
                menu.IsOpen = false;

                m_chromeHidden = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("HideShellChrome: " + ex.Message);
            }
        }

        /// <summary>
        /// Puts the navigation pane and hamburger button back the way they were.
        /// </summary>
        private void RestoreShellChrome()
        {
            if (!m_chromeHidden)
            {
                return;
            }
            m_chromeHidden = false;

            try
            {
                if (Shell.Instance == null)
                {
                    return;
                }
                var menu = Shell.HamburgerMenu;
                if (menu == null)
                {
                    return;
                }

                menu.HamburgerButtonVisibility = m_savedHamburgerVisibility;
                menu.DisplayMode = m_savedDisplayMode;
                menu.VisualStateNarrowDisplayMode = m_savedNarrowDisplayMode;
                menu.VisualStateNormalDisplayMode = m_savedNormalDisplayMode;
                menu.VisualStateWideDisplayMode = m_savedWideDisplayMode;
                menu.IsOpen = m_savedPaneOpen;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RestoreShellChrome: " + ex.Message);
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
                // The pointer may already be sitting on an edge when capture
                // starts - warp now, or it is stuck there with no further
                // movement events to trigger a warp later.
                TryRecenterPointer(pos);
                return;
            }

            double rawDx = pos.X - m_lastX;
            double rawDy = pos.Y - m_lastY;
            m_lastX = pos.X;
            m_lastY = pos.Y;

            if (m_recenterPending)
            {
                if (Math.Abs(rawDx) >= c_jumpThresholdDip || Math.Abs(rawDy) >= c_jumpThresholdDip)
                {
                    // The teleport landed - this jump must not reach the phone.
                    m_recenterPending = false;
                    return;
                }

                if (unchecked(Environment.TickCount - m_recenterPendingSince) > c_recenterTimeoutMs)
                {
                    // Injection never landed. Drop the stale baseline so the
                    // next event resyncs instead of snapping the camera.
                    m_recenterPending = false;
                    m_haveLastPos = false;
                    return;
                }

                // Real movement while the teleport is in flight still counts.
            }

            double dx = rawDx * m_sensitivity;
            double dy = rawDy * m_sensitivity;

            if (dx != 0 || dy != 0)
            {
                lock (m_stateLock)
                {
                    m_accX += (int)Math.Round(dx);
                    m_accY += (int)Math.Round(dy);
                }
            }

            if (!m_recenterPending)
            {
                TryRecenterPointer(pos);
            }
        }

        /// <summary>
        /// Pulls the OS pointer back to the middle of the window when it comes
        /// near an edge, so relative movement never runs out of room and the
        /// pointer never pokes the top-edge title-bar overlay.
        /// </summary>
        /// <param name="pos">Latest pointer position in window DIPs.</param>
        private void TryRecenterPointer(Windows.Foundation.Point pos)
        {
            var bounds = Window.Current.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            bool nearEdge =
                pos.X <= c_edgeMarginDip ||
                pos.X >= bounds.Width - c_edgeMarginDip ||
                pos.Y <= c_edgeMarginDip ||
                pos.Y >= bounds.Height - c_edgeMarginDip;
            if (!nearEdge)
            {
                return;
            }

            // Win32 first: SetCursorPos lands exactly on the monitor centre, on
            // any monitor layout. Injected *relative* moves are scaled by the
            // Windows mouse ballistics and overshoot, so they are not used here.
            if (Models.SystemPointer.TryWarpToMonitorCenter())
            {
                MarkRecenterStarted();
                return;
            }

            // Fallback: absolute injection, normalised 0-65535 against the
            // primary display (32768,32768 is its centre - verified). Wrong
            // monitor on a multi-monitor desk, but only reached when user32 is
            // unavailable.
            if (m_injector == null)
            {
                return;
            }

            try
            {
                var info = new InjectedInputMouseInfo
                {
                    MouseOptions = InjectedInputMouseOptions.Move | InjectedInputMouseOptions.Absolute,
                    DeltaX = c_normalizedCenter,
                    DeltaY = c_normalizedCenter,
                };
                m_injector.InjectMouseInput(new List<InjectedInputMouseInfo> { info });
                MarkRecenterStarted();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Pointer recenter failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Records that a warp is in flight so the resulting position jump is
        /// dropped instead of being reported to the phone as movement.
        /// </summary>
        private void MarkRecenterStarted()
        {
            m_recenterPending = true;
            m_recenterPendingSince = Environment.TickCount;
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
        /// Keeps every key away from the PC's own window while capturing.
        /// XAML accelerators, Tab focus moves, access keys and text input all
        /// arrive through the dispatcher first, so marking the event handled
        /// here is what stops shortcuts from firing.
        /// </summary>
        /// <param name="sender">Core dispatcher.</param>
        /// <param name="args">Accelerator args.</param>
        private void OnAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            args.Handled = true;

            // The exit hotkey is handled here as well as in OnKeyDown so it
            // still works if the CoreWindow route is suppressed. ExitCapture is
            // idempotent.
            if (args.EventType == CoreAcceleratorKeyEventType.KeyDown ||
                args.EventType == CoreAcceleratorKeyEventType.SystemKeyDown)
            {
                if (IsExitHotkey(args.VirtualKey) && !args.KeyStatus.WasKeyDown)
                {
                    ExitCapture();
                }
            }
        }

        /// <summary>
        /// Swallows text input so nothing can type into the window.
        /// </summary>
        /// <param name="sender">Core window.</param>
        /// <param name="args">Character args.</param>
        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            args.Handled = true;
        }

        /// <summary>
        /// Local capture-page shortcuts plus keyboard forwarding to the phone.
        /// </summary>
        /// <param name="sender">Core window.</param>
        /// <param name="args">Key args.</param>
        private void OnKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            // Nothing should act on the PC's window - including this very key.
            args.Handled = true;

            // The exit hotkey is reserved to leave capture mode and is NOT
            // forwarded to the phone, so a game cannot trap the user.
            if (IsExitHotkey(args.VirtualKey))
            {
                if (!args.KeyStatus.WasKeyDown)
                {
                    ExitCapture();
                }
                return;
            }

            // F1 toggles the help overlay (Tab is forwarded to the game instead).
            if (args.VirtualKey == VirtualKey.F1)
            {
                Overlay.Visibility = Overlay.Visibility == Windows.UI.Xaml.Visibility.Visible
                    ? Windows.UI.Xaml.Visibility.Collapsed
                    : Windows.UI.Xaml.Visibility.Visible;
                return;
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
                uint scanCode = Models.HidHelper.GetPs2Set1ScanCodeFromStatus(args.KeyStatus);
                m_forwardedPresses.Add(scanCode);
                kb.PressKey(scanCode);
            }
        }

        /// <summary>
        /// Forwards key releases to the phone, but only for keys whose press we
        /// forwarded - so a locally-consumed key (exit hotkey, F1, ...) cannot
        /// leave a stuck key on the phone either.
        /// </summary>
        /// <param name="sender">Core window.</param>
        /// <param name="args">Key args.</param>
        private void OnKeyUp(CoreWindow sender, KeyEventArgs args)
        {
            args.Handled = true;

            uint scanCode = Models.HidHelper.GetPs2Set1ScanCodeFromStatus(args.KeyStatus);
            if (!m_forwardedPresses.Remove(scanCode))
            {
                // Press was never forwarded (exit hotkey / F1 / numpad +/-).
                return;
            }

            var kb = Models.VirtualKeyboard.Current;
            if (kb != null)
            {
                kb.ReleaseKey(scanCode);
            }
        }

        /// <summary>
        /// True when the key completes the configured exit hotkey.
        /// </summary>
        /// <param name="key">Virtual key that was pressed.</param>
        /// <returns>True to leave capture mode.</returns>
        private bool IsExitHotkey(VirtualKey key)
        {
            var hotkey = m_exitHotkey ?? Models.CaptureHotkey.Default;
            return hotkey.Matches(key, Window.Current != null ? Window.Current.CoreWindow : null);
        }

        /// <summary>
        /// Leaves capture mode exactly once, even if both key routes fire.
        /// </summary>
        private void ExitCapture()
        {
            if (m_exiting)
            {
                return;
            }
            m_exiting = true;

            if (Frame != null && Frame.CanGoBack)
            {
                Frame.GoBack();
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
