// <copyright file="SettingsPage.xaml.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
using System;
using Windows.ApplicationModel.Core;
using Windows.Globalization;
using Windows.Storage;
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
