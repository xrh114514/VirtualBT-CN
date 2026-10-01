// <copyright file="SafeDispatcher.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
// Thread-safe dispatcher access.
//
// WHY THIS EXISTS: CoreApplication.MainView.CoreWindow.Dispatcher is not safe to
// touch from a background thread on Windows 11 24H2+. It can fail-fast with
// 0x800710DF (ERROR_NO_TASK_QUEUE) inside combase.dll and kill the process.
// The BLE watchers and GATT request callbacks raise events on thread-pool
// threads, so the old pattern (used throughout this sample) crashed as soon as
// a scan produced results or a central read a characteristic.
//
// Fix: capture the CoreDispatcher once on the UI thread, then use that instance
// from anywhere. HasThreadAccess lets us avoid a hop when we are already on UI.
//
// This lives in GattServicesLibrary (not the app project) because the GATT
// read/write callbacks in GenericGattCharacteristic need it too.
//----------------------------------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace GattServicesLibrary.Helpers
{
    /// <summary>
    /// Cached dispatcher helper safe to call from any thread.
    /// </summary>
    public static class SafeDispatcher
    {
        private static CoreDispatcher s_dispatcher;
        private static readonly object s_lock = new object();

        /// <summary>
        /// Call once from the UI thread (App constructor / OnLaunched) before any
        /// watcher or GATT callbacks can fire.
        /// </summary>
        /// <param name="dispatcher">The UI thread dispatcher.</param>
        public static void Initialize(CoreDispatcher dispatcher)
        {
            lock (s_lock)
            {
                s_dispatcher = dispatcher;
            }
        }

        /// <summary>
        /// Gets the cached UI dispatcher. Safe from any thread.
        /// Call <see cref="Initialize"/> from the UI thread first; otherwise this
        /// falls back to the current thread's CoreWindow when already on UI.
        /// </summary>
        /// <returns>The UI-thread CoreDispatcher, or null if none is available yet.</returns>
        public static CoreDispatcher Current
        {
            get
            {
                return Dispatcher;
            }
        }

        /// <summary>
        /// Gets the cached dispatcher, falling back to the current thread's
        /// CoreWindow only when we are already on the UI thread.
        /// </summary>
        private static CoreDispatcher Dispatcher
        {
            get
            {
                lock (s_lock)
                {
                    if (s_dispatcher != null)
                    {
                        return s_dispatcher;
                    }
                }

                // Fallback: if we happen to be on the UI thread already, CoreWindow
                // is safe here and lets us initialize lazily.
                try
                {
                    var window = CoreWindow.GetForCurrentThread();
                    if (window != null)
                    {
                        lock (s_lock)
                        {
                            if (s_dispatcher == null)
                            {
                                s_dispatcher = window.Dispatcher;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Never touch CoreApplication.MainView from here - that is what crashes.
                }

                lock (s_lock)
                {
                    return s_dispatcher;
                }
            }
        }

        /// <summary>
        /// Runs the action on the UI thread. Executes inline when already there.
        /// Silently no-ops when no dispatcher is available yet (never crashes).
        /// </summary>
        /// <param name="action">Action to run on the UI thread.</param>
        /// <returns>A task that completes when the action has run.</returns>
        public static async Task RunAsync(Action action)
        {
            if (action == null)
            {
                return;
            }

            CoreDispatcher dispatcher = Dispatcher;
            if (dispatcher == null)
            {
                // No UI thread yet - drop the work rather than fail-fast.
                return;
            }

            if (dispatcher.HasThreadAccess)
            {
                try
                {
                    action();
                }
                catch (Exception)
                {
                    // Same contract as the dispatched path: never let a UI update
                    // exception escape into a background-thread async void.
                }
                return;
            }

            try
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
            }
            catch (Exception)
            {
                // Dispatcher shut down mid-flight (page navigation etc). Drop it.
            }
        }

        /// <summary>
        /// Runs the async action on the UI thread and awaits it. Executes inline
        /// when already on the UI thread.
        /// </summary>
        /// <param name="action">Async action to run on the UI thread.</param>
        /// <returns>A task that completes when the action has completed.</returns>
        public static async Task RunAsync(Func<Task> action)
        {
            if (action == null)
            {
                return;
            }

            CoreDispatcher dispatcher = Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            if (dispatcher.HasThreadAccess)
            {
                try
                {
                    await action();
                }
                catch (Exception)
                {
                    // Same contract as the dispatched path - swallow, never crash.
                }
                return;
            }

            try
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    try
                    {
                        await action();
                    }
                    catch (Exception)
                    {
                        // Swallow - matches previous behaviour of fire-and-forget dispatch.
                    }
                });
            }
            catch (Exception)
            {
                // Dispatcher shut down mid-flight. Drop it.
            }
        }
    }
}
