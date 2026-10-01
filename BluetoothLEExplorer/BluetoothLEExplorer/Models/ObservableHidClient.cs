using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using System.Diagnostics;

namespace BluetoothLEExplorer.Models
{
    public class ObservableGattClient : INotifyPropertyChanged, IEquatable<ObservableGattClient>
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private BluetoothLEDevice m_leDevice;

        private bool m_isConnected;
        public bool IsConnected
        {
            get
            {
                return m_isConnected;
            }
            private set
            {
                m_isConnected = value;
                OnPropertyChanged("IsConnected");
                OnPropertyChanged("ConnectionText");
            }
        }

        /// <summary>
        /// Gets the localized connection state as a plain string.
        /// The page binds TextBlock.Text (a string) to this. Do not bind a
        /// converter that returns a <see cref="LocalizedString"/> to a string
        /// property instead: x:Bind casts the converter result and a
        /// LocalizedString object throws InvalidCastException, which killed
        /// the process as soon as a phone connected.
        /// </summary>
        public string ConnectionText
        {
            get
            {
                return m_isConnected
                    ? Loc.Get("Str_ConnectedLS.Value")
                    : Loc.Get("Str_DisconnectedLS.Value");
            }
        }

        public string Name
        {
            get
            {
                return m_leDevice.Name;
            }
        }

        public static async Task<ObservableGattClient> FromIdAsync(string deviceId)
        {
            var leDevice = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (leDevice == null)
            {
                // Device vanished between the subscribe event and this lookup
                // (or the id is not resolvable). Returning null is safe for the
                // caller; constructing around null is not - it throws inside an
                // async void and kills the process.
                return null;
            }
            return new ObservableGattClient(leDevice);
        }

        public ObservableGattClient(BluetoothLEDevice device)
        {
            m_leDevice = device;
            m_leDevice.ConnectionStatusChanged += ConnectionStatusChanged;
            IsConnected = (m_leDevice.ConnectionStatus == BluetoothConnectionStatus.Connected);
        }

        // NOTE: there is deliberately no finalizer here. Calling into WinRT
        // (ConnectionStatusChanged -= , Dispose) from the .NET finalizer thread
        // throws E_NOTIMPL out of combase.dll and kills the process - that was
        // one of the connect-time crashes. The device and this wrapper reference
        // each other and are collected together; nothing needs finalizing.

        bool IEquatable<ObservableGattClient>.Equals(ObservableGattClient other)
        {
            if (other == null)
            {
                return false;
            }

            return (m_leDevice.DeviceId == other.m_leDevice.DeviceId);
        }

        private void ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            // WinRT event handler on a thread-pool thread - guard it, see above.
            try
            {
                IsConnected = (sender.ConnectionStatus == BluetoothConnectionStatus.Connected);
            }
            catch (Exception e)
            {
                Debug.WriteLine("ConnectionStatusChanged: " + e.Message);
            }
        }

        private async void OnPropertyChanged(string propertyName)
        {
            try
            {
                if (PropertyChanged != null)
                {
                    await SafeDispatcher.Current.RunAsync(
                        Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () =>
                        {
                            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
                        });
                }
            }
            catch (Exception e)
            {
                Debug.Fail(String.Format("Failed to update property '{0}' due to {1}", propertyName, e.ToString()));
            }
        }
    }
}
