// <copyright file="CaptureHotkey.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
// The configurable hotkey that leaves the fullscreen mouse-capture page.
// Stored in LocalSettings as "Ctrl+112"-style text (modifiers by name, key by
// numeric value) so it survives restarts and can be rebound from the settings
// page. Kept out of the page code-behind because the settings page needs the
// same parse/display logic.
//----------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;

namespace BluetoothLEExplorer.Models
{
    /// <summary>
    /// Hotkey (optional Ctrl/Alt/Shift plus one key) used to leave mouse capture.
    /// </summary>
    public sealed class CaptureHotkey
    {
        /// <summary>LocalSettings key holding the serialized hotkey.</summary>
        public const string SettingKey = "MouseCaptureExitHotkey";

        /// <summary>True when Ctrl must be down.</summary>
        public bool Ctrl { get; set; }

        /// <summary>True when Shift must be down.</summary>
        public bool Shift { get; set; }

        /// <summary>True when Alt must be down.</summary>
        public bool Alt { get; set; }

        /// <summary>The non-modifier key of the chord.</summary>
        public VirtualKey Key { get; set; }

        /// <summary>
        /// The hotkey used until the user picks another one (Esc), matching the
        /// behaviour of versions before the key became configurable.
        /// </summary>
        /// <returns>A fresh default hotkey.</returns>
        public static CaptureHotkey Default
        {
            get { return new CaptureHotkey { Key = VirtualKey.Escape }; }
        }

        /// <summary>
        /// Reads the saved hotkey.
        /// </summary>
        /// <returns>The saved hotkey, or <see cref="Default"/> when unset or unparseable.</returns>
        public static CaptureHotkey Load()
        {
            try
            {
                object v = ApplicationData.Current.LocalSettings.Values[SettingKey];
                return Parse(v as string) ?? Default;
            }
            catch (Exception)
            {
                return Default;
            }
        }

        /// <summary>Persists the hotkey so it survives restarts.</summary>
        public void Save()
        {
            try
            {
                ApplicationData.Current.LocalSettings.Values[SettingKey] = Serialize();
            }
            catch (Exception)
            {
                // A failed save must not take the settings page down; the next
                // launch simply falls back to the default key.
            }
        }

        /// <summary>
        /// Parses the "Ctrl+Shift+112" text produced by <see cref="Serialize"/>.
        /// </summary>
        /// <param name="text">Serialized hotkey.</param>
        /// <returns>The hotkey, or null when the text is empty or unusable.</returns>
        public static CaptureHotkey Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var hotkey = new CaptureHotkey();
            bool haveKey = false;
            foreach (string rawPart in text.Split('+'))
            {
                string part = rawPart.Trim();
                if (part.Length == 0)
                {
                    continue;
                }

                if (string.Equals(part, "Ctrl", StringComparison.OrdinalIgnoreCase))
                {
                    hotkey.Ctrl = true;
                }
                else if (string.Equals(part, "Shift", StringComparison.OrdinalIgnoreCase))
                {
                    hotkey.Shift = true;
                }
                else if (string.Equals(part, "Alt", StringComparison.OrdinalIgnoreCase))
                {
                    hotkey.Alt = true;
                }
                else
                {
                    VirtualKey key;
                    if (!TryParseKey(part, out key))
                    {
                        return null;
                    }
                    hotkey.Key = key;
                    haveKey = true;
                }
            }

            return haveKey ? hotkey : null;
        }

        /// <summary>
        /// Serializes to "Ctrl+Shift+112"-style text. The key is stored as its
        /// numeric value on purpose: <c>Enum.Parse</c>/<c>Enum.GetName</c> need
        /// reflection metadata that .NET Native does not guarantee.
        /// </summary>
        /// <returns>Serialized form.</returns>
        public string Serialize()
        {
            var parts = new List<string>();
            if (Ctrl)
            {
                parts.Add("Ctrl");
            }
            if (Shift)
            {
                parts.Add("Shift");
            }
            if (Alt)
            {
                parts.Add("Alt");
            }
            parts.Add(((int)Key).ToString());
            return string.Join("+", parts);
        }

        /// <summary>
        /// Short form shown on the settings button and in the capture-page hint.
        /// </summary>
        /// <returns>e.g. "Esc" or "Ctrl+F12".</returns>
        public string Display()
        {
            var parts = new List<string>();
            if (Ctrl)
            {
                parts.Add("Ctrl");
            }
            if (Shift)
            {
                parts.Add("Shift");
            }
            if (Alt)
            {
                parts.Add("Alt");
            }
            parts.Add(FriendlyKeyName(Key));
            return string.Join("+", parts);
        }

        /// <summary>
        /// True when a key press matches this hotkey. The modifiers the hotkey
        /// requires must be down; extra modifiers are allowed, so the exit chord
        /// still works when Shift/Ctrl happens to be held - being able to leave
        /// capture mode matters more than an exact match.
        /// </summary>
        /// <param name="key">Virtual key of the key that was pressed.</param>
        /// <param name="window">Window used to read the modifier state.</param>
        /// <returns>True on a match.</returns>
        public bool Matches(VirtualKey key, CoreWindow window)
        {
            if (key != Key)
            {
                return false;
            }

            if (window == null)
            {
                return !Ctrl && !Shift && !Alt;
            }

            if (Ctrl && !IsDown(window, VirtualKey.Control))
            {
                return false;
            }
            if (Shift && !IsDown(window, VirtualKey.Shift))
            {
                return false;
            }
            if (Alt && !IsDown(window, VirtualKey.Menu))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// True for keys that only act as modifiers and can never bind a hotkey.
        /// </summary>
        /// <param name="key">Virtual key to test.</param>
        /// <returns>True for Ctrl/Shift/Alt/Win in either position.</returns>
        public static bool IsModifierKey(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.Control:
                case VirtualKey.Shift:
                case VirtualKey.Menu:
                case VirtualKey.LeftControl:
                case VirtualKey.RightControl:
                case VirtualKey.LeftShift:
                case VirtualKey.RightShift:
                case VirtualKey.LeftMenu:
                case VirtualKey.RightMenu:
                case VirtualKey.LeftWindows:
                case VirtualKey.RightWindows:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsDown(CoreWindow window, VirtualKey key)
        {
            return (window.GetKeyState(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }

        /// <summary>
        /// Human-readable key name, without reflection (see <see cref="Serialize"/>).
        /// </summary>
        /// <param name="key">Key to name.</param>
        /// <returns>e.g. "Esc", "F12", "A", "Num5".</returns>
        private static string FriendlyKeyName(VirtualKey key)
        {
            int code = (int)key;

            // Letters and digits have obvious names.
            if (code >= (int)VirtualKey.A && code <= (int)VirtualKey.Z)
            {
                return ((char)('A' + code - (int)VirtualKey.A)).ToString();
            }
            if (code >= (int)VirtualKey.Number0 && code <= (int)VirtualKey.Number9)
            {
                return ((char)('0' + code - (int)VirtualKey.Number0)).ToString();
            }

            switch (key)
            {
                case VirtualKey.Escape: return "Esc";
                case VirtualKey.Tab: return "Tab";
                case VirtualKey.Enter: return "Enter";
                case VirtualKey.Space: return "Space";
                case VirtualKey.Back: return "Backspace";
                case VirtualKey.CapitalLock: return "CapsLock";
                case VirtualKey.Scroll: return "ScrollLock";
                case VirtualKey.Pause: return "Pause";
                case VirtualKey.Snapshot: return "PrtSc";
                case VirtualKey.Insert: return "Ins";
                case VirtualKey.Delete: return "Del";
                case VirtualKey.Home: return "Home";
                case VirtualKey.End: return "End";
                case VirtualKey.PageUp: return "PgUp";
                case VirtualKey.PageDown: return "PgDn";
                case VirtualKey.Left: return "Left";
                case VirtualKey.Right: return "Right";
                case VirtualKey.Up: return "Up";
                case VirtualKey.Down: return "Down";
                case VirtualKey.Add: return "Num+";
                case VirtualKey.Subtract: return "Num-";
                case VirtualKey.Multiply: return "Num*";
                case VirtualKey.Divide: return "Num/";
                case VirtualKey.Decimal: return "Num.";
                default:
                    if (code >= (int)VirtualKey.NumberPad0 && code <= (int)VirtualKey.NumberPad9)
                    {
                        return "Num" + (code - (int)VirtualKey.NumberPad0).ToString();
                    }
                    if (code >= (int)VirtualKey.F1 && code <= (int)VirtualKey.F24)
                    {
                        return "F" + (code - (int)VirtualKey.F1 + 1).ToString();
                    }
                    return code.ToString();
            }
        }

        private static bool TryParseKey(string text, out VirtualKey key)
        {
            // Only the numeric form is written (see Serialize), but accept it
            // however it arrives.
            int numeric;
            if (int.TryParse(text, out numeric))
            {
                key = (VirtualKey)numeric;
                return true;
            }
            key = VirtualKey.None;
            return false;
        }
    }
}
