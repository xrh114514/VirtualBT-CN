// <copyright file="App.xaml.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using BluetoothLEExplorer.Services.SettingsServices;
using Template10.Controls;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Popups;
using BluetoothLEExplorer.Models;
using BluetoothLEExplorer.ViewModels;
using System.Diagnostics;

namespace BluetoothLEExplorer
{
    //// Documentation on APIs used in this page: 
    //// https://github.com/Windows-XAML/Template10/wiki

    /// <summary>
    /// The application
    /// </summary>
    [Bindable]
    public sealed partial class App : Template10.Common.BootStrapper
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="App" /> class.
        /// </summary>
        public App()
        {
            InitializeComponent();
            this.UnhandledException += App_UnhandledException;

            // WinRT callback exceptions (GATT events, connection-status handlers,
            // anything invoked from the BLE stack on a thread-pool thread) do NOT
            // reach Application.UnhandledException - they are "stowed" and the
            // process dies with 0xc000027b in Windows.UI.Xaml.dll. This event is
            // the only place to see the real exception, and setting Propagate to
            // false stops the crash so the app survives and we can diagnose.
            Windows.ApplicationModel.Core.CoreApplication.UnhandledErrorDetected += App_UnhandledErrorDetected;

            this.Suspending += App_Suspending;
            this.Resuming += App_Resuming;

            SplashFactory = (e) => new Views.Splash(e);

            #region App settings

            var settings = SettingsService.Instance;
            RequestedTheme = settings.AppTheme;
            CacheMaxDuration = settings.CacheMaxDuration;
            ShowShellBackButton = settings.UseShellBackButton;

            #endregion
        }

        private void App_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            try
            {
                var deferral = e.SuspendingOperation.GetDeferral();
 
                foreach(GenericGattServiceViewModel service in GattSampleContext.Context.CreatedServices)
                {
                    string key = "Service_"+ service.Service.ServiceProvider.Service.Uuid.ToString() + "_IsPublishing";
                    bool value = service.IsPublishing;
                    SettingsService.Instance.SettingsDictionary[key] = value;

                    if (service.IsPublishing)
                    {
                        service.Service.ServiceProvider.StopAdvertising();
                    }
                }

                GattSampleContext.Context.ReleaseAllResources();

                deferral.Complete();
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Suspending: " + ex.Message);
            }
        }

        private void App_Resuming(object sender, object e)
        {
            string[] keys = new string[SettingsService.Instance.SettingsDictionary.Keys.Count];

            SettingsService.Instance.SettingsDictionary.Keys.CopyTo(keys, 0);

            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].Contains("Service_"))
                {
                    string serviceUUID = keys[i].Split('_')[1];
                    bool IsPublishing = (bool)SettingsService.Instance.SettingsDictionary[keys[i]];

                    if (IsPublishing)
                    {
                        foreach (GenericGattServiceViewModel service in GattSampleContext.Context.CreatedServices)
                        {
                            if (serviceUUID == service.Service.ServiceProvider.Service.Uuid.ToString())
                            {
                                service.Start();
                            }
                        }
                    }
                }
            }
        }

        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogCrash("XAML UnhandledException", e.Exception);
            // Keep the app alive - a dead process helps nobody.
            e.Handled = true;
        }

        private void App_UnhandledErrorDetected(object sender, Windows.ApplicationModel.Core.UnhandledErrorDetectedEventArgs e)
        {
            try
            {
                // Propagate() re-raises the error as a catchable exception.
                // Catching it here marks the error as handled, so the process
                // does not terminate - without this the app just vanishes and
                // the event log only shows 0xc000027b in Windows.UI.Xaml.dll.
                e.UnhandledError.Propagate();
                LogCrash("WinRT UnhandledErrorDetected", "Propagate() returned without throwing");
            }
            catch (Exception ex)
            {
                LogCrash("WinRT UnhandledErrorDetected", ex.ToString());
            }
        }

        private static void LogCrash(string kind, object detail)
        {
            string text = "[" + DateTimeOffset.Now.ToString("o") + "] " + kind + "\r\n" + detail + "\r\n====\r\n";
            try
            {
                Debug.WriteLine(text);
                var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
                string path = System.IO.Path.Combine(folder.Path, "unhandled-error.log");
                System.IO.File.AppendAllText(path, text);
            }
            catch (Exception)
            {
                // Logging must never throw.
            }
        }

        private async void showDialog(string content)
        {
            MessageDialog dialog = new MessageDialog(content, Models.Loc.Get("Str_FatalError.Text"));
            await dialog.ShowAsync();
        }

        /// <summary>
        /// Application initialization
        /// </summary>
        /// <param name="args"></param>
        /// <returns>On initialization task</returns>
        public override async Task OnInitializeAsync(IActivatedEventArgs args)
        {
            // Cache the UI dispatcher now. Watcher callbacks arrive on thread-pool
            // threads and must never call CoreApplication.MainView.CoreWindow.Dispatcher
            // (that fails-fast with 0x800710DF on Windows 11 24H2+).
            // Apply the language the user chose last time (before any page text
            // is created). See SettingsPage for the switcher.
            Views.SettingsPage.ApplyStoredLanguage();

            Models.SafeDispatcher.Initialize(Window.Current.Dispatcher);

            if (Window.Current.Content as ModalDialog == null)
            {
                // create a new frame 
                var nav = NavigationServiceFactory(BackButton.Attach, ExistingContent.Include);

                // create modal root
                Window.Current.Content = new ModalDialog
                {
                    DisableBackButtonWhenModal = true,
                    Content = new Views.Shell(nav),
                    ModalContent = new Views.Busy(),
                };
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// App initialization for long running tasks
        /// </summary>
        /// <param name="startKind"></param>
        /// <param name="args"></param>
        /// <returns>On start task</returns>
        public override async Task OnStartAsync(StartKind startKind, IActivatedEventArgs args)
        {
            //// long-running startup tasks go here

            NavigationService.Navigate(typeof(Views.Discover));
            await Task.CompletedTask;
        }
    }
}
