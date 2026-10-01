using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Input;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Background;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Power;
using Windows.Storage.Streams;

namespace BluetoothLEExplorer.Models
{
    class VirtualKeyboard
    {
        /// <summary>
        /// The single keyboard/mouse HID instance owned by the app.
        /// Set by <see cref="InitiliazeAsync"/> so other pages can send reports.
        /// </summary>
        public static VirtualKeyboard Current { get; private set; }

        /// <summary>
        /// True while the fullscreen capture page owns keyboard input. The
        /// keyboard page's CoreWindow handlers must then stay silent so keys are
        /// not reported twice.
        /// </summary>
        public static bool CapturePageOwnsKeyboard { get; set; }


        private static readonly GattLocalCharacteristicParameters c_hidInputReportParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify,
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired
        };

        private static readonly uint c_hidReportReferenceDescriptorShortUuid = 0x2908;

        private static readonly GattLocalDescriptorParameters c_hidKeyboardReportReferenceParameters = new GattLocalDescriptorParameters
        {
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
            StaticValue = new byte[]
            {
                0x01, // Report ID: 1
                0x01  // Report Type: Input
            }.AsBuffer()
        };

        private static readonly GattLocalDescriptorParameters c_hidMouseReportReferenceParameters = new GattLocalDescriptorParameters
        {
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
            StaticValue = new byte[]
            {
                0x02, // Report ID: 2
                0x01  // Report Type: Input
            }.AsBuffer()
        };

        // HID report map: keyboard (Report ID 1) + mouse (Report ID 2).
        // Mouse payload (no report id byte): buttons, X lo, X hi, Y lo, Y hi, wheel.
        private static readonly GattLocalCharacteristicParameters c_hidReportMapParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read,
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
            StaticValue = new byte[]
            {
                // ---------- Keyboard ----------
                0x05, 0x01,       // USAGE_PAGE (Generic Desktop)
                0x09, 0x06,       // USAGE (Keyboard)
                0xa1, 0x01,       // COLLECTION (Application)
                0x05, 0x07,       //   USAGE_PAGE (Keyboard)
                0x85, 0x01,       //   REPORT_ID (1)
                0x19, 0xe0,       //   USAGE_MINIMUM (Keyboard LeftControl)
                0x29, 0xe7,       //   USAGE_MAXIMUM (Keyboard Right GUI)
                0x15, 0x00,       //   LOGICAL_MINIMUM (0)
                0x25, 0x01,       //   LOGICAL_MAXIMUM (1)
                0x75, 0x01,       //   REPORT_SIZE (1)
                0x95, 0x08,       //   REPORT_COUNT (8)
                0x81, 0x02,       //   INPUT (Data,Var,Abs)
                0x75, 0x08,       //   REPORT_SIZE (8)
                0x95, 0x07,       //   REPORT_COUNT (7)
                0x19, 0x00,       //   USAGE_MINIMUM (Reserved)
                0x29, 0x65,       //   USAGE_MAXIMUM (Keyboard Application)
                0x15, 0x00,       //   LOGICAL_MINIMUM (0)
                0x25, 0x65,       //   LOGICAL_MAXIMUM (101)
                0x81, 0x00,       //   INPUT (Data,Ary,Abs)
                0xc0,             // END_COLLECTION

                // ---------- Mouse ----------
                0x05, 0x01,       // USAGE_PAGE (Generic Desktop)
                0x09, 0x02,       // USAGE (Mouse)
                0xa1, 0x01,       // COLLECTION (Application)
                0x85, 0x02,       //   REPORT_ID (2)
                0x09, 0x01,       //   USAGE (Pointer)
                0xa1, 0x00,       //   COLLECTION (Physical)
                0x05, 0x09,       //     USAGE_PAGE (Buttons)
                0x19, 0x01,       //     USAGE_MINIMUM (1)
                0x29, 0x03,       //     USAGE_MAXIMUM (3)
                0x15, 0x00,       //     LOGICAL_MINIMUM (0)
                0x25, 0x01,       //     LOGICAL_MAXIMUM (1)
                0x95, 0x03,       //     REPORT_COUNT (3)
                0x75, 0x01,       //     REPORT_SIZE (1)
                0x81, 0x02,       //     INPUT (Data,Var,Abs)
                0x95, 0x01,       //     REPORT_COUNT (1)
                0x75, 0x05,       //     REPORT_SIZE (5)  ; pad
                0x81, 0x01,       //     INPUT (Const,Arr,Abs)
                0x05, 0x01,       //     USAGE_PAGE (Generic Desktop)
                0x09, 0x30,       //     USAGE (X)
                0x09, 0x31,       //     USAGE (Y)
                0x16, 0x00, 0x80, //     LOGICAL_MINIMUM (-32768)
                0x26, 0xff, 0x7f, //     LOGICAL_MAXIMUM (32767)
                0x75, 0x10,       //     REPORT_SIZE (16)
                0x95, 0x02,       //     REPORT_COUNT (2)
                0x81, 0x06,       //     INPUT (Data,Var,Rel)
                0x09, 0x38,       //     USAGE (Wheel)
                0x15, 0x81,       //     LOGICAL_MINIMUM (-127)
                0x25, 0x7f,       //     LOGICAL_MAXIMUM (127)
                0x75, 0x08,       //     REPORT_SIZE (8)
                0x95, 0x01,       //     REPORT_COUNT (1)
                0x81, 0x06,       //     INPUT (Data,Var,Rel)
                0xc0,             //   END_COLLECTION
                0xc0,             // END_COLLECTION
            }.AsBuffer()
        };

        private static readonly GattLocalCharacteristicParameters c_hidInformationParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read,
            ReadProtectionLevel = GattProtectionLevel.EncryptionRequired,
            StaticValue = new byte[]
            {
                0x11, 0x01, // HID Version: 1101
                0x00,       // Country Code: 0
                0x01        // Not Normally Connectable, Remote Wake supported
            }.AsBuffer()
        };

        private static readonly GattLocalCharacteristicParameters c_hidControlPointParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.WriteWithoutResponse,
            WriteProtectionLevel = GattProtectionLevel.EncryptionRequired
        };

        private static readonly GattLocalCharacteristicParameters c_batteryLevelParameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify,
            ReadProtectionLevel = GattProtectionLevel.Plain
        };

        private static readonly uint c_sizeOfKeyboardReportDataInBytes = 0x8;
        private static readonly uint c_sizeOfMouseReportDataInBytes = 0x6;

        private GattServiceProvider m_hidServiceProvider;
        private GattLocalService m_hidService;
        private GattLocalCharacteristic m_hidKeyboardReport;
        private GattLocalDescriptor m_hidKeyboardReportReference;
        private GattLocalCharacteristic m_hidMouseReport;
        private GattLocalDescriptor m_hidMouseReportReference;
        private GattLocalCharacteristic m_hidReportMap;
        private GattLocalCharacteristic m_hidInformation;
        private GattLocalCharacteristic m_hidControlPoint;

        private Object m_lock = new Object();

        private bool m_initializationFinished = false;

        private HashSet<byte> m_currentlyDepressedModifierKeys = new HashSet<byte>();
        private HashSet<byte> m_currentlyDepressedKeys = new HashSet<byte>();
        private byte[] m_lastSentKeyboardReportValue = new byte[c_sizeOfKeyboardReportDataInBytes];

        public delegate void SubscribedHidClientsChangedHandler(IReadOnlyList<GattSubscribedClient> subscribedClients);
        public event SubscribedHidClientsChangedHandler SubscribedHidClientsChanged;

        private static string GetStringFromBuffer(IBuffer buffer)
        {
            return GetStringFromBuffer(buffer.ToArray());
        }

        private static string GetStringFromBuffer(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", " ");
        }

        public async Task InitiliazeAsync()
        {
            Current = this;
            await CreateHidService();

            lock (m_lock)
            {
                m_initializationFinished = true;
            }
        }

        public void Enable()
        {
            PublishService(m_hidServiceProvider);
        }

        public void Disable()
        {
            UnpublishService(m_hidServiceProvider);
        }

        /// <summary>
        /// Sends a relative mouse movement / button state report to all
        /// subscribed HID clients (the phone). Payload layout:
        /// [buttons, X lo, X hi, Y lo, Y hi, wheel]
        /// </summary>
        /// <param name="buttons">Bitfield: 0x01=left, 0x02=right, 0x04=middle.</param>
        /// <param name="deltaX">Relative X movement (negative = left).</param>
        /// <param name="deltaY">Relative Y movement (negative = up).</param>
        /// <param name="wheel">Vertical wheel delta.</param>
        public void SendMouseReport(byte buttons, short deltaX, short deltaY, sbyte wheel)
        {
            lock (m_lock)
            {
                if (!m_initializationFinished)
                {
                    return;
                }

                if (m_hidMouseReport == null || m_hidMouseReport.SubscribedClients.Count == 0)
                {
                    return;
                }

                var reportValue = new byte[c_sizeOfMouseReportDataInBytes];
                reportValue[0] = buttons;
                reportValue[1] = (byte)(deltaX & 0xff);
                reportValue[2] = (byte)((deltaX >> 8) & 0xff);
                reportValue[3] = (byte)(deltaY & 0xff);
                reportValue[4] = (byte)((deltaY >> 8) & 0xff);
                reportValue[5] = (byte)wheel;

                // Fire and forget - ordering is guaranteed per client and
                // waiting for the notification adds noticeable lag in games.
                var asyncOp = m_hidMouseReport.NotifyValueAsync(reportValue.AsBuffer());
            }
        }

        /// <summary>
        /// True when at least one HID client is subscribed (i.e. the phone is
        /// connected and can receive reports).
        /// </summary>
        public bool HasSubscribedClients
        {
            get
            {
                lock (m_lock)
                {
                    return m_initializationFinished &&
                           (m_hidKeyboardReport != null && m_hidKeyboardReport.SubscribedClients.Count > 0 ||
                            m_hidMouseReport != null && m_hidMouseReport.SubscribedClients.Count > 0);
                }
            }
        }

        public void PressKey(uint ps2Set1keyScanCode)
        {
            try
            {
                ChangeKeyState(KeyEvent.KeyMake, HidHelper.GetHidUsageFromPs2Set1(ps2Set1keyScanCode));
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to change the key state due to: " + e.Message);
            }
        }

        public void ReleaseKey(uint ps2Set1keyScanCode)
        {
            try
            {
                ChangeKeyState(KeyEvent.KeyBreak, HidHelper.GetHidUsageFromPs2Set1(ps2Set1keyScanCode));
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to change the key state due to: " + e.Message);
            }
        }

        private async Task CreateHidService()
        {
            // RadioNotAvailable is often transient - the radio may still be
            // initialising, or another app (Phone Link / nRF Connect / etc.) may
            // be holding the BLE advertising resources. Retry before giving up.
            const int maxAttempts = 6;
            GattServiceProviderResult hidServiceProviderCreationResult = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                hidServiceProviderCreationResult = await GattServiceProvider.CreateAsync(GattServiceUuids.HumanInterfaceDevice);
                if (hidServiceProviderCreationResult != null &&
                    hidServiceProviderCreationResult.Error == BluetoothError.Success)
                {
                    break;
                }

                Debug.WriteLine("CreateAsync HID attempt " + attempt + " failed: " +
                    (hidServiceProviderCreationResult != null ? hidServiceProviderCreationResult.Error.ToString() : "null"));
                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }

            if (hidServiceProviderCreationResult == null ||
                hidServiceProviderCreationResult.Error != BluetoothError.Success)
            {
                var err = hidServiceProviderCreationResult != null
                    ? hidServiceProviderCreationResult.Error
                    : BluetoothError.OtherError;
                throw new Exception(DescribeGattServiceError(err));
            }
            m_hidServiceProvider = hidServiceProviderCreationResult.ServiceProvider;
            m_hidService = m_hidServiceProvider.Service;

            // HID keyboard Report characteristic.
            var hidKeyboardReportCharacteristicCreationResult = await m_hidService.CreateCharacteristicAsync(GattCharacteristicUuids.Report, c_hidInputReportParameters);
            if (hidKeyboardReportCharacteristicCreationResult.Error != BluetoothError.Success)
            {
                Debug.WriteLine("无法创建键盘报告特征：" + hidKeyboardReportCharacteristicCreationResult.Error);
                throw new Exception("无法创建键盘报告特征：" + hidKeyboardReportCharacteristicCreationResult.Error);
            }
            m_hidKeyboardReport = hidKeyboardReportCharacteristicCreationResult.Characteristic;
            m_hidKeyboardReport.SubscribedClientsChanged += HidKeyboardReport_SubscribedClientsChanged;
            if (m_hidMouseReport != null)
            {
                m_hidMouseReport.SubscribedClientsChanged += HidKeyboardReport_SubscribedClientsChanged;
            }

            // HID keyboard Report Reference descriptor.
            var hidKeyboardReportReferenceCreationResult = await m_hidKeyboardReport.CreateDescriptorAsync(BluetoothUuidHelper.FromShortId(c_hidReportReferenceDescriptorShortUuid), c_hidKeyboardReportReferenceParameters);
            if (hidKeyboardReportReferenceCreationResult.Error != BluetoothError.Success)
            {
                Debug.WriteLine("无法创建键盘报告引用描述符：" + hidKeyboardReportReferenceCreationResult.Error);
                throw new Exception("无法创建键盘报告引用描述符：" + hidKeyboardReportReferenceCreationResult.Error);
            }
            m_hidKeyboardReportReference = hidKeyboardReportReferenceCreationResult.Descriptor;

            // HID mouse Report characteristic (Report ID 2).
            var hidMouseReportCreationResult = await m_hidService.CreateCharacteristicAsync(GattCharacteristicUuids.Report, c_hidInputReportParameters);
            if (hidMouseReportCreationResult.Error != BluetoothError.Success)
            {
                Debug.WriteLine("Failed to create the mouse report characteristic: " + hidMouseReportCreationResult.Error);
                throw new Exception("Failed to create the mouse report characteristic: " + hidMouseReportCreationResult.Error);
            }
            m_hidMouseReport = hidMouseReportCreationResult.Characteristic;

            // HID mouse Report Reference descriptor.
            var hidMouseReportReferenceCreationResult = await m_hidMouseReport.CreateDescriptorAsync(BluetoothUuidHelper.FromShortId(c_hidReportReferenceDescriptorShortUuid), c_hidMouseReportReferenceParameters);
            if (hidMouseReportReferenceCreationResult.Error != BluetoothError.Success)
            {
                Debug.WriteLine("Failed to create the mouse report reference descriptor: " + hidMouseReportReferenceCreationResult.Error);
                throw new Exception("Failed to create the mouse report reference descriptor: " + hidMouseReportReferenceCreationResult.Error);
            }
            m_hidMouseReportReference = hidMouseReportReferenceCreationResult.Descriptor;

            // HID Report Map characteristic.
            var hidReportMapCharacteristicCreationResult = await m_hidService.CreateCharacteristicAsync(GattCharacteristicUuids.ReportMap, c_hidReportMapParameters);
            if (hidReportMapCharacteristicCreationResult.Error != BluetoothError.Success)
            {
                Debug.WriteLine("无法创建HID报告映射特征：" + hidReportMapCharacteristicCreationResult.Error);
                throw new Exception("无法创建HID报告映射特征：" + hidReportMapCharacteristicCreationResult.Error);
            }
            m_hidReportMap = hidReportMapCharacteristicCreationResult.Characteristic;

            // HID Information characteristic.
            var hidInformationCharacteristicCreationResult = await m_hidService.CreateCharacteristicAsync(GattCharacteristicUuids.HidInformation, c_hidInformationParameters);
            if (hidInformationCharacteristicCreationResult.Error != BluetoothError.Success)
            {
                Debug.WriteLine("无法创建HID信息特征：" + hidInformationCharacteristicCreationResult.Error);
                throw new Exception("无法创建HID信息特征：" + hidInformationCharacteristicCreationResult.Error);
            }
            m_hidInformation = hidInformationCharacteristicCreationResult.Characteristic;

            // HID Control Point characteristic.
            var hidControlPointCharacteristicCreationResult = await m_hidService.CreateCharacteristicAsync(GattCharacteristicUuids.HidControlPoint, c_hidControlPointParameters);
            if (hidControlPointCharacteristicCreationResult.Error != BluetoothError.Success)
            {
                Debug.WriteLine("无法创建HID控制点特征：" + hidControlPointCharacteristicCreationResult.Error);
                throw new Exception("无法创建HID控制点特征：" + hidControlPointCharacteristicCreationResult.Error);
            }
            m_hidControlPoint = hidControlPointCharacteristicCreationResult.Characteristic;
            m_hidControlPoint.WriteRequested += HidControlPoint_WriteRequested;

            m_hidServiceProvider.AdvertisementStatusChanged += HidServiceProvider_AdvertisementStatusChanged;
        }

        // Assumes that the lock is being held.
        private void PublishService(GattServiceProvider provider)
        {
            var advertisingParameters = new GattServiceProviderAdvertisingParameters
            {
                IsDiscoverable = true,
                IsConnectable = true // Peripheral role support is required for Windows to advertise as connectable.
            };

            provider.StartAdvertising(advertisingParameters);
        }

        private void UnpublishService(GattServiceProvider provider)
        {
            try
            {
                if ((provider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started) ||
                    (provider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Aborted))
                {
                    provider.StopAdvertising();
                    SubscribedHidClientsChanged?.Invoke(null);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to stop advertising due to: " + e.Message);
            }
        }

        private async void HidControlPoint_WriteRequested(GattLocalCharacteristic sender, GattWriteRequestedEventArgs args)
        {
            // WinRT event handler on a thread-pool thread - guard it. The
            // deferral must always be completed or the central's ATT write hangs.
            var deferral = args.GetDeferral();
            try
            {
                // GetRequestAsync has to run on the UX thread (see the note in
                // GenericGattCharacteristic.Characteristic_ReadRequested).
                string logged = null;
                await SafeDispatcher.RunAsync(async () =>
                {
                    var writeRequest = await args.GetRequestAsync();
                    logged = GetStringFromBuffer(writeRequest.Value);
                });
                if (logged != null)
                {
                    Debug.WriteLine("Value written to HID Control Point: " + logged);
                }
                // Control point only supports WriteWithoutResponse.
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to handle write to Hid Control Point due to: " + e.Message);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void HidServiceProvider_AdvertisementStatusChanged(GattServiceProvider sender, GattServiceProviderAdvertisementStatusChangedEventArgs args)
        {
            Debug.WriteLine("HID advertisement status changed to " + args.Status);
        }

        private void BatteryServiceProvider_AdvertisementStatusChanged(GattServiceProvider sender, GattServiceProviderAdvertisementStatusChangedEventArgs args)
        {
            Debug.WriteLine("Battery advertisement status changed to " + args.Status);
        }

        private void HidKeyboardReport_SubscribedClientsChanged(GattLocalCharacteristic sender, object args)
        {
            // WinRT event handler on a thread-pool thread: an exception escaping
            // here is stowed and kills the process (0xc000027b), so the whole
            // body is guarded.
            try
            {
                // Report the union of keyboard + mouse subscribers so the UI list
                // shows every connected client regardless of which report it uses.
                var all = new List<GattSubscribedClient>();
                lock (m_lock)
                {
                    if (m_hidKeyboardReport != null)
                    {
                        foreach (var c in m_hidKeyboardReport.SubscribedClients)
                        {
                            all.Add(c);
                        }
                    }
                    if (m_hidMouseReport != null)
                    {
                        foreach (var c in m_hidMouseReport.SubscribedClients)
                        {
                            bool dup = false;
                            foreach (var existing in all)
                            {
                                if (existing.Session.DeviceId.Id == c.Session.DeviceId.Id)
                                {
                                    dup = true;
                                    break;
                                }
                            }
                            if (!dup)
                            {
                                all.Add(c);
                            }
                        }
                    }
                }

                Debug.WriteLine("Number of clients now registered for HID notifications: " + all.Count);
                SubscribedHidClientsChanged?.Invoke(all);
            }
            catch (Exception e)
            {
                Debug.WriteLine("SubscribedClientsChanged: " + e.Message);
            }
        }

        private void ChangeKeyState(KeyEvent keyEvent, byte hidUsageScanCode)
        {
            lock (m_lock)
            {
                if (!m_initializationFinished)
                {
                    return;
                }

                if (keyEvent == KeyEvent.KeyMake)
                {
                    if (HidHelper.IsMofifierKey(hidUsageScanCode))
                    {
                        Debug.WriteLine("Modifier key depressed: " + hidUsageScanCode);
                        m_currentlyDepressedModifierKeys.Add(hidUsageScanCode);
                    }
                    else
                    {
                        Debug.WriteLine("Key depressed: " + hidUsageScanCode);
                        m_currentlyDepressedKeys.Add(hidUsageScanCode);
                    }
                }
                else
                {
                    if (HidHelper.IsMofifierKey(hidUsageScanCode))
                    {
                        Debug.WriteLine("Modifier key released: " + hidUsageScanCode);
                        m_currentlyDepressedModifierKeys.Remove(hidUsageScanCode);
                    }
                    else
                    {
                        Debug.WriteLine("Key released: " + hidUsageScanCode);
                        m_currentlyDepressedKeys.Remove(hidUsageScanCode);
                    }
                }

                if (m_hidKeyboardReport.SubscribedClients.Count == 0)
                {
                    Debug.WriteLine("No clients are currently subscribed to the keyboard report.");
                    return;
                }

                var reportValue = new byte[c_sizeOfKeyboardReportDataInBytes];

                // The first byte of the report data is a modifier key bitfield.
                reportValue[0] = 0x0;
                foreach (var modifierKeyPressedScanCode in m_currentlyDepressedModifierKeys)
                {
                    reportValue[0] |= HidHelper.GetFlagOfModifierKey(modifierKeyPressedScanCode);
                }

                // The second byte up to the last byte represent one key per byte.
                int reportIndex = 1;
                foreach (var keyPressedScanCode in m_currentlyDepressedKeys)
                {
                    if (reportIndex >= reportValue.Length)
                    {
                        Debug.WriteLine("Too many keys currently depressed to fit into the report data. Truncating.");
                        break;
                    }

                    reportValue[reportIndex] = keyPressedScanCode;
                    reportIndex++;
                }

                if (!reportValue.SequenceEqual(m_lastSentKeyboardReportValue))
                {
                    Debug.WriteLine("Sending keyboard report value notification with data: " + GetStringFromBuffer(reportValue));
                    reportValue.CopyTo(m_lastSentKeyboardReportValue, 0);

                    // Waiting for this operation to complete is no longer necessary since now ordering of notifications
                    // is guaranteed for each client. Not waiting for it to complete reduces delays and lags.
                    // Note that doing this makes us unable to know if the notification failed to be sent.
                    var asyncOp = m_hidKeyboardReport.NotifyValueAsync(reportValue.AsBuffer());
                }
            }
        }

        /// <summary>
        /// Turns a GattServiceProvider creation error into an actionable
        /// Chinese explanation for the user.
        /// </summary>
        /// <param name="error">BluetoothError from CreateAsync.</param>
        /// <returns>Message body.</returns>
        /// <summary>
        /// Checks whether a Bluetooth radio is present and switched on.
        /// Returns true when a Bluetooth radio is ON, false when it is off or
        /// missing, and null when the state cannot be determined.
        /// </summary>
        private static bool? TryGetBluetoothRadioIsOn()
        {
            try
            {
                var op = Windows.Devices.Radios.Radio.GetRadiosAsync();
                var radios = op.AsTask().GetAwaiter().GetResult();
                bool sawBluetooth = false;
                foreach (var radio in radios)
                {
                    if (radio.Kind == Windows.Devices.Radios.RadioKind.Bluetooth)
                    {
                        sawBluetooth = true;
                        if (radio.State == Windows.Devices.Radios.RadioState.On)
                        {
                            return true;
                        }
                    }
                }
                return sawBluetooth ? (bool?)false : (bool?)null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string DescribeGattServiceError(BluetoothError error)
        {
            if (error == BluetoothError.RadioNotAvailable)
            {
                bool? radioOn = TryGetBluetoothRadioIsOn();
                var parts = new List<string>();
                parts.Add(Loc.Get("Str_HidRadioOff.Text"));
                parts.Add("");
                if (radioOn == false)
                {
                    parts.Add(Loc.Get("Str_HidRadioOff.Text"));
                    parts.Add(Loc.Get("Str_HidRadioOffFix.Text"));
                }
                else if (radioOn == true)
                {
                    parts.Add(Loc.Get("Str_HidRadioOnStillFails.Text"));
                    parts.Add(Loc.Get("Str_HidSteps.Text"));
                }
                else
                {
                    parts.Add(Loc.Get("Str_HidSteps.Text"));
                }
                parts.Add("");
                parts.Add(Loc.Get("Str_HidNeedPeripheral.Text"));
                return string.Join(Environment.NewLine, parts);
            }
            var other = new string[]
            {
                Loc.Get("Str_HidRadioOnStillFails.Text") + " " + error.ToString(),
                "",
                Loc.Get("Str_HidSteps.Text"),
            };
            return string.Join(Environment.NewLine, other);
        }



    }
}
