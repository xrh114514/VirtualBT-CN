// <copyright file="LocalizedString.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------------------------
// A tiny wrapper used where XAML needs a localizable string as a *value*
// (e.g. ValueWhenConverter.Value). x:Uid only sets dependency properties on
// an element, and <x:String> has none, so we substitute this class: its
// Value property is filled from .resw via x:Uid.
//
// LIMITATION - do not bind a converter that returns this type to a STRING
// property (TextBlock.Text, etc). x:Bind casts the converter result to the
// target property type, so a LocalizedString object throws
// InvalidCastException and the process dies (this was the "crash when a phone
// connects" bug - see ObservableGattClient.ConnectionText). It is only safe
// for object-typed targets such as Button.Content, which display via
// ToString(). For a string target, expose a string property and look the text
// up with Loc.Get.
//----------------------------------------------------------------------------------------------
using System;

namespace BluetoothLEExplorer.Models
{
    /// <summary>
    /// Localizable string value usable inside XAML resource dictionaries.
    /// </summary>
    public sealed class LocalizedString
    {
        /// <summary>
        /// Gets or sets the localized text. Set from .resw through x:Uid.
        /// </summary>
        /// <value>The text to display.</value>
        public string Value { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Value ?? string.Empty;
        }
    }
}
