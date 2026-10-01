// <copyright file="SettingsPage.xaml.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
using System;
using Windows.ApplicationModel.Core;
using Windows.Globalization;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BluetoothLEExplorer.Views
{
    /// <summary>
    /// Settings page
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        /// <summary>LocalSettings key holding the chosen language tag.</summary>
        private const string c_languageSettingKey = "AppLanguage";


        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsPage" /> class.
        /// </summary>
        /// <summary>
        /// True while the combo is being populated programmatically. Set BEFORE
        /// InitializeComponent so its SelectionChanged events are ignored.
        /// </summary>
        private bool m_initializing = true;

        /// <summary>Hotkey that leaves the mouse-capture page.</summary>
        private Models.CaptureHotkey m_exitHotkey;

        /// <summary>True while waiting for the user to press the new hotkey.</summary>
        private bool m_bindingHotkey;

        public SettingsPage()
        {
            m_initializing = true;
            InitializeComponent();
        }

        /// <summary>
        /// Gets the language tag currently stored in settings (defaults to zh-CN).
        /// </summary>
        /// <returns>Language tag such as "zh-CN" or "en-US".</returns>
        public static string CurrentLanguageTag
        {
            get
            {
                var values = ApplicationData.Current.LocalSettings;
                object v = values.Values[c_languageSettingKey];
                return v as string ?? "zh-CN";
            }
        }

        /// <summary>
        /// Applies the stored language at startup. Must be called before any UI
        /// text is loaded.
        /// </summary>
        public static void ApplyStoredLanguage()
        {
            // Only apply an explicitly saved choice. If the user has never picked
            // a language we must NOT force a default here - that would clobber
            // an override set by the switcher right before a restart.
            object v = ApplicationData.Current.LocalSettings.Values[c_languageSettingKey];
            string tag = v as string;
            if (!string.IsNullOrEmpty(tag))
            {
                ApplicationLanguages.PrimaryLanguageOverride = tag;
            }
        }

        /// <summary>
        /// Executes when navigating to settings page
        /// </summary>
        /// <param name="e">Navigation args.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            m_initializing = true;
            string lang = CurrentLanguageTag;
            foreach (var item in LanguageCombo.Items)
            {
                var cbi = item as ComboBoxItem;
                if (cbi != null && string.Equals(cbi.Tag as string, lang, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageCombo.SelectedItem = cbi;
                    break;
                }
            }
            m_initializing = false;

            m_exitHotkey = Models.CaptureHotkey.Load();
            RefreshExitHotkeyButton();

            // Select the saved fullscreen presentation. Same guard as the
            // language combo: a SelectionChanged raised while populating would
            // write the first item back over the saved choice.
            m_initializing = true;
            string mode = Models.FullscreenMode.IsExclusive()
                ? Models.FullscreenMode.ExclusiveValue
                : Models.FullscreenMode.BorderlessValue;
            foreach (var item in FullscreenModeCombo.Items)
            {
                var cbi = item as ComboBoxItem;
                if (cbi != null && string.Equals(cbi.Tag as string, mode, StringComparison.OrdinalIgnoreCase))
                {
                    FullscreenModeCombo.SelectedItem = cbi;
                    break;
                }
            }
            m_initializing = false;
        }

        /// <summary>
        /// Saves the fullscreen presentation of the mouse-capture page.
        /// Takes effect the next time capture mode is entered.
        /// </summary>
        /// <param name="sender">Combo box.</param>
        /// <param name="e">Selection args.</param>
        private void FullscreenModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_initializing)
            {
                return;
            }

            var cbi = FullscreenModeCombo.SelectedItem as ComboBoxItem;
            if (cbi == null)
            {
                return;
            }

            bool exclusive = string.Equals(cbi.Tag as string, Models.FullscreenMode.ExclusiveValue, StringComparison.OrdinalIgnoreCase);
            Models.FullscreenMode.Set(exclusive);
        }

        /// <inheritdoc />
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Never leave a dangling key hook if the user navigates away
            // mid-binding.
            StopHotkeyBinding();
            base.OnNavigatedFrom(e);
        }

        /// <summary>
        /// Puts the button back into its idle state, showing the current hotkey.
        /// </summary>
        private void RefreshExitHotkeyButton()
        {
            if (ExitHotkeyButton != null)
            {
                ExitHotkeyButton.Content = (m_exitHotkey ?? Models.CaptureHotkey.Default).Display();
            }
        }

        /// <summary>
        /// Starts listening for the key that should leave mouse capture.
        /// </summary>
        /// <param name="sender">Button.</param>
        /// <param name="e">Click args.</param>
        private void ExitHotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (m_bindingHotkey)
            {
                // Already listening. Clicking again (e.g. Space on the focused
                // button while binding) must not restart and lose the key.
                return;
            }

            m_bindingHotkey = true;
            ExitHotkeyButton.Content = Models.Loc.Get("Str_ExitHotkeyPress.Content");
            Window.Current.CoreWindow.KeyDown += OnHotkeyKeyDown;
        }

        /// <summary>
        /// Restores the default exit hotkey (Esc).
        /// </summary>
        /// <param name="sender">Button.</param>
        /// <param name="e">Click args.</param>
        private void ExitHotkeyReset_Click(object sender, RoutedEventArgs e)
        {
            var hotkey = Models.CaptureHotkey.Default;
            hotkey.Save();
            m_exitHotkey = hotkey;
            StopHotkeyBinding();
        }

        /// <summary>
        /// Captures the next real key press as the new exit hotkey.
        /// </summary>
        /// <param name="sender">Core window.</param>
        /// <param name="args">Key args.</param>
        private void OnHotkeyKeyDown(CoreWindow sender, KeyEventArgs args)
        {
            // The key is being recorded - it must not also activate whatever
            // control happens to have focus (Space on the button, and so on).
            args.Handled = true;

            if (Models.CaptureHotkey.IsModifierKey(args.VirtualKey))
            {
                // Wait for the key the modifiers are held with.
                return;
            }

            var hotkey = new Models.CaptureHotkey
            {
                Ctrl = IsDown(VirtualKey.Control),
                Shift = IsDown(VirtualKey.Shift),
                Alt = IsDown(VirtualKey.Menu),
                Key = args.VirtualKey,
            };
            hotkey.Save();
            m_exitHotkey = hotkey;
            StopHotkeyBinding();
        }

        /// <summary>
        /// Leaves key-recording mode and restores the button label.
        /// </summary>
        private void StopHotkeyBinding()
        {
            if (m_bindingHotkey)
            {
                m_bindingHotkey = false;
                Window.Current.CoreWindow.KeyDown -= OnHotkeyKeyDown;
            }
            RefreshExitHotkeyButton();
        }

        /// <summary>
        /// True when the modifier key is currently held.
        /// </summary>
        /// <param name="key">Modifier virtual key.</param>
        /// <returns>True when down.</returns>
        private static bool IsDown(VirtualKey key)
        {
            var window = Window.Current != null ? Window.Current.CoreWindow : null;
            return window != null &&
                   (window.GetKeyState(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }

        /// <summary>
        /// Switches the UI language and restarts the app so every page reloads
        /// its resources.
        /// </summary>
        /// <param name="sender">Combo box.</param>
        /// <param name="e">Selection args.</param>
        private async void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (m_initializing)
            {
                return;
            }

            var cbi = LanguageCombo.SelectedItem as ComboBoxItem;
            if (cbi == null)
            {
                return;
            }

            string tag = cbi.Tag as string;
            if (string.IsNullOrEmpty(tag) || string.Equals(tag, CurrentLanguageTag, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Persist first and verify - a failed save would make the restart
            // silently fall back to the previous language.
            var values = ApplicationData.Current.LocalSettings.Values;
            values[c_languageSettingKey] = tag;
            object check = values[c_languageSettingKey];
            if (!string.Equals(check as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                // Retry: clear then set again.
                values.Remove(c_languageSettingKey);
                values[c_languageSettingKey] = tag;
                check = values[c_languageSettingKey];
            }

            ApplicationLanguages.PrimaryLanguageOverride = tag;

            if (!string.Equals(check as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                // Could not persist - tell the user instead of restarting blind.
                var dlg = new Windows.UI.Popups.MessageDialog(
                    "Failed to save the language setting: " + check,
                    "Error");
                await dlg.ShowAsync();
                return;
            }

            // Restart so every page and dialog picks up the new resources.
            await CoreApplication.RequestRestartAsync(string.Empty);
        }
    }
}
