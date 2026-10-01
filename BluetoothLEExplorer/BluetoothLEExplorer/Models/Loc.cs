// <copyright file="Loc.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
// Thin wrapper over ResourceLoader so code-behind / dialogs can pull the same
// strings the XAML gets through x:Uid. Keys match the .resw entries
// (e.g. "Str_FatalError.Text").
//----------------------------------------------------------------------------------------------
using System;
using Windows.ApplicationModel.Resources;

namespace BluetoothLEExplorer.Models
{
    /// <summary>
    /// Localized string lookup for C# code.
    /// </summary>
    public static class Loc
    {
        private static ResourceLoader s_loader;

        private static ResourceLoader Loader
        {
            get
            {
                if (s_loader == null)
                {
                    s_loader = new ResourceLoader();
                }
                return s_loader;
            }
        }

        /// <summary>
        /// Looks up a localized string.
        /// </summary>
        /// <param name="key">Full .resw key, e.g. "Str_FatalError.Text".</param>
        /// <returns>The localized value, or the key when missing.</returns>
        public static string Get(string key)
        {
            try
            {
                // The compiled resource map nests uid/Property with a slash,
                // so translate the resw-style dot form before lookup.
                string v = Loader.GetString(key.Replace('.', '/'));
                return string.IsNullOrEmpty(v) ? key : v;
            }
            catch (Exception)
            {
                return key;
            }
        }

        /// <summary>
        /// Looks up a localized string and formats arguments into it.
        /// </summary>
        /// <param name="key">Full .resw key.</param>
        /// <param name="args">Format arguments.</param>
        /// <returns>Formatted localized text.</returns>
        public static string Format(string key, params object[] args)
        {
            string fmt = Get(key);
            try
            {
                return string.Format(fmt, args);
            }
            catch (Exception)
            {
                return fmt;
            }
        }
    }
}
