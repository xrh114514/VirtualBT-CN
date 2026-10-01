// <copyright file="SafeDispatcher.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
// Thread-safe dispatcher access.
//
// The implementation lives in GattServicesLibrary.Helpers.SafeDispatcher so the
// GATT request callbacks in GenericGattCharacteristic (a lower-layer project)
// can use the same cache. This facade keeps the existing
// BluetoothLEExplorer.Models.SafeDispatcher call sites working unchanged.
//
// See the real implementation for the why: CoreApplication.MainView.CoreWindow.
// Dispatcher fail-fasts from background threads on Windows 11 24H2+.
//----------------------------------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace BluetoothLEExplorer.Models
{
    /// <summary>
    /// Cached dispatcher helper safe to call from any thread.
    /// Delegates to <see cref="GattServicesLibrary.Helpers.SafeDispatcher"/>.
    /// </summary>
    public static class SafeDispatcher
    {
        /// <summary>
        /// Call once from the UI thread (App constructor / OnLaunched) before any
        /// watcher callbacks can fire.
        /// </summary>
        /// <param name="dispatcher">The UI thread dispatcher.</param>
        public static void Initialize(CoreDispatcher dispatcher)
        {
            GattServicesLibrary.Helpers.SafeDispatcher.Initialize(dispatcher);
        }

        /// <summary>
        /// Gets the cached UI dispatcher. Safe from any thread.
        /// </summary>
        /// <returns>The UI-thread CoreDispatcher, or null if none is available yet.</returns>
        public static CoreDispatcher Current
        {
            get
            {
                return GattServicesLibrary.Helpers.SafeDispatcher.Current;
            }
        }

        /// <summary>
        /// Runs the action on the UI thread. Executes inline when already there.
        /// </summary>
        /// <param name="action">Action to run on the UI thread.</param>
        /// <returns>A task that completes when the action has run.</returns>
        public static Task RunAsync(Action action)
        {
            return GattServicesLibrary.Helpers.SafeDispatcher.RunAsync(action);
        }

        /// <summary>
        /// Runs the async action on the UI thread and awaits it.
        /// </summary>
        /// <param name="action">Async action to run on the UI thread.</param>
        /// <returns>A task that completes when the action has completed.</returns>
        public static Task RunAsync(Func<Task> action)
        {
            return GattServicesLibrary.Helpers.SafeDispatcher.RunAsync(action);
        }
    }
}
