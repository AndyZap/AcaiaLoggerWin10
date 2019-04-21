using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Popups;

namespace AcaiaLogger
{
    public sealed partial class MainPage : Page
    {
        private string deviceIdAcaia = String.Empty;
        private string deviceIdTesto = String.Empty;

        private BluetoothCacheMode bluetoothCacheMode = BluetoothCacheMode.Cached;

        private BluetoothLEDevice bluetoothDeviceScale = null;
        private BluetoothLEDevice bluetoothDeviceTesto = null;

        private GattCharacteristic characteristicScale = null;
        private GattCharacteristic characteristicTestoWrite = null;
        private GattCharacteristic characteristicTestoNotif = null;

        private DispatcherTimer heartBeatTimer;

        private enum StatusEnum { Disabled, Disconnected, Discovered, CharacteristicConnected }

        private StatusEnum statusScale = StatusEnum.Disconnected;
        private StatusEnum statusTesto = StatusEnum.Disconnected;

        private bool subscribedForNotificationsScale = false;
        private bool subscribedForNotificationsTesto = false;

        public MainPage()
        {
            this.InitializeComponent();

            heartBeatTimer = new DispatcherTimer();
            heartBeatTimer.Tick += dispatcherTimer_Tick;
            heartBeatTimer.Interval = new TimeSpan(0, 0, 3);

            NotifyUser("", NotifyType.StatusMessage);

            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            var val = localSettings.Values["DeviceIdAcaia"] as string;
            deviceIdAcaia = val == null? "" : val;

            val = localSettings.Values["DeviceIdTesto"] as string;
            deviceIdTesto = val == null ? "" : val;

            val = localSettings.Values["DetailBeansName"] as string;
            DetailBeansName.Text = val == null ? "" : val;

            val = localSettings.Values["DetailGrind"] as string;
            DetailGrind.Text = val == null ? "" : val;

            val = localSettings.Values["EnableTesto"] as string;
            ChkTesto.IsOn = val == null ? false : val == "true";
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            List<string> scenarios = new List<string> { ">  Log A Brew", ">  Brew details", ">  Brew history" };

            ScenarioControl.ItemsSource = scenarios;
            if (Window.Current.Bounds.Width < 640)
                ScenarioControl.SelectedIndex = -1;
            else
                ScenarioControl.SelectedIndex = 0;

            LoadLog();

            ResultsListView.ItemsSource = BrewLog;

            PanelConnectDisconnect.Background = new SolidColorBrush(Windows.UI.Colors.Yellow);
        }

        private void ScenarioControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox scenarioListBox = sender as ListBox;
            if (scenarioListBox.SelectedIndex == 0)  // >  Log A Brew
            {
                PanelBrewDetails.Visibility = Visibility.Collapsed;
                ScrollViewerBrewList.Visibility = Visibility.Collapsed;
                PanelLogBrew.Visibility = Visibility.Visible;
            }
            else if (scenarioListBox.SelectedIndex == 1) // >  Brew details
            {
                PanelLogBrew.Visibility = Visibility.Collapsed;
                ScrollViewerBrewList.Visibility = Visibility.Collapsed;
                PanelBrewDetails.Visibility = Visibility.Visible;
            }
            else if (scenarioListBox.SelectedIndex == 2)  // >  Brew history
            {
                PanelLogBrew.Visibility = Visibility.Collapsed;
                PanelBrewDetails.Visibility = Visibility.Collapsed;
                ScrollViewerBrewList.Visibility = Visibility.Visible;
            }
            else
                NotifyUser("Unknown menu item", NotifyType.ErrorMessage);
        }

        public void FatalError(string message)
        {
            NotifyUser(message, NotifyType.ErrorMessage);
            Disconnect();
        }

        public void NotifyUser(string strMessage, NotifyType type)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatus(strMessage, type);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, type));
            }
        }

        public void NotifyWeight(double weight_gramm)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateWeight(weight_gramm);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateWeight(weight_gramm));
            }
        }
        public void NotifyPressure(double pressure_bar)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdatePressure(pressure_bar);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdatePressure(pressure_bar));
            }
        }

        private void UpdateStatus(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }

            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }

            // Raise an event if necessary to enable a screen reader to announce the status update.
            var peer = FrameworkElementAutomationPeer.FromElement(StatusBlock);
            if (peer != null)
            {
                peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        }

        DateTime startTimeWeight = DateTime.MinValue;
        private void UpdateWeight(double weight_gramm)
        {
            LogBrewWeight.Text = weight_gramm == double.MinValue ? "---" : weight_gramm.ToString("0.0");

            // Raise an event if necessary to enable a screen reader to announce the status update.
            var peer = FrameworkElementAutomationPeer.FromElement(LogBrewWeight);
            if (peer != null)
            {
                peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }

            if (startTimeWeight != DateTime.MinValue)
            {
                var tspan = (DateTime.Now - startTimeWeight);

                if (tspan.TotalSeconds >= 60)
                    LogBrewTime.Text = tspan.Minutes.ToString("0") + ":" + tspan.Seconds.ToString("00");
                else
                    LogBrewTime.Text = tspan.Seconds.ToString("0");

                var peerT = FrameworkElementAutomationPeer.FromElement(LogBrewTime);
                if (peerT != null)
                {
                    peerT.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                }
            }

            if (!weightEverySec.NewReading(weight_gramm))
                FatalError("Error: do not receive regular weight measurements from the scale");
        }

        private void UpdatePressure(double pressure_bar)
        {
            LogBrewPressure.Text = pressure_bar == double.MinValue ? "---" : pressure_bar.ToString("0.0");

            // Raise an event if necessary to enable a screen reader to announce the status update.
            var peer = FrameworkElementAutomationPeer.FromElement(LogBrewPressure);
            if (peer != null)
            {
                peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }

            if (!pressureEverySec.NewReading(pressure_bar))
                FatalError("Error: do not receive regular pressure measurements from T549i");
        }

        private void MenuToggleButton_Click(object sender, RoutedEventArgs e)
        {
            Splitter.IsPaneOpen = !Splitter.IsPaneOpen;
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            ScenarioControl.SelectedIndex = 0;

            NotifyUser("Connecting ... ", NotifyType.StatusMessage);

            BtnConnect.IsEnabled = false;
            BtnDisconnect.IsEnabled = true;

            bool need_device_watcher = false;

            //  ===========   ACAIA  ==================

            statusScale = StatusEnum.Disconnected;

            if(true)  // scale is always enabled (but testo could be disabled)
            {
                if (deviceIdAcaia != String.Empty) // try to connect if we already know the DeviceID
                {
                    try
                    {
                        bluetoothDeviceScale = await BluetoothLEDevice.FromIdAsync(deviceIdAcaia);
                    }
                    catch (Exception) { }
                }

                if (bluetoothDeviceScale == null) // Failed to connect with the device ID, need to search for the scale
                {
                    if (deviceWatcher == null)
                        need_device_watcher = true;
                }
                else // we have bluetoothLeDevice, connect to the characteristic
                {
                    statusScale = StatusEnum.Discovered;
                }
            }

            //  ===========   TESTO  ==================

            statusTesto = ChkTesto.IsOn ? StatusEnum.Disconnected : StatusEnum.Disabled;

            if (statusTesto != StatusEnum.Disabled)
            {
                if (deviceIdTesto != String.Empty) // try to connect if we already know the DeviceID
                {
                    try
                    {
                        bluetoothDeviceTesto = await BluetoothLEDevice.FromIdAsync(deviceIdTesto);
                    }
                    catch (Exception) { }
                }

                if (bluetoothDeviceTesto == null) // Failed to connect with the device ID, need to search for testo
                {
                    if (deviceWatcher == null)
                        need_device_watcher = true;
                }
                else // we have bluetoothLeDevice, connect to the characteristic
                {
                    statusTesto = StatusEnum.Discovered;
                }
            }

            if (need_device_watcher)
            {
                StartBleDeviceWatcher();
                NotifyUser("Device watcher started", NotifyType.StatusMessage);
            }

            heartBeatTimer.Start();
        }

        async void dispatcherTimer_Tick(object sender, object e)
        {
            heartBeatTimer.Stop();

            // Commmon actions from scale and testo
            bool device_watcher_needs_stopping = false;
            string message_scale = "";
            string message_testo = "";

            //  ===========   ACAIA  ==================

            if (statusScale == StatusEnum.Disabled)
            {
                // do nothing
            }
            else if (statusScale == StatusEnum.Disconnected)
            {
                foreach (var d in KnownDevices)
                {
                    if (d.Name.StartsWith("PROCH") || d.Name.StartsWith("ACAIA"))
                    {
                        deviceIdAcaia = d.Id;

                        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                        localSettings.Values["DeviceIdAcaia"] = deviceIdAcaia;

                        statusScale = StatusEnum.Discovered;

                        device_watcher_needs_stopping = true;

                        message_scale = "Discovered " + deviceIdAcaia + " ";

                        break;
                    }
                }
            }
            else if (statusScale == StatusEnum.Discovered)
            {
                try
                {
                    if (bluetoothDeviceScale == null)
                    {
                        try
                        {
                            bluetoothDeviceScale = await BluetoothLEDevice.FromIdAsync(deviceIdAcaia);
                        }
                        catch (Exception) { }
                    }

                    if (bluetoothDeviceScale == null)
                    {
                        FatalError("Failed to create Acaia BluetoothLEDevice");
                        return;
                    }

                    GattDeviceServicesResult result_service = await bluetoothDeviceScale.GetGattServicesForUuidAsync(
                    new Guid(ScaleServiceGuid), bluetoothCacheMode);

                    if (result_service.Status != GattCommunicationStatus.Success)
                    {
                        FatalError("Failed to get Scale service 0x1820 " + result_service.Status.ToString());
                        return;
                    }

                    if (result_service.Services.Count != 1)
                    {
                        FatalError("Error, expected to find one Scale service 0x1820");
                        return;
                    }

                    var service = result_service.Services[0];

                    // Ensure we have access to the device.
                    var accessStatus = await service.RequestAccessAsync();

                    if (accessStatus != DeviceAccessStatus.Allowed)
                    {
                        FatalError("Do not have access to the Scale service 0x1820");
                        return;
                    }

                    // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
                    // and the new Async functions to get the characteristics of unpaired devices as well. 

                    GattCharacteristicsResult result_charact = await service.GetCharacteristicsForUuidAsync(new Guid(ScaleCharactGuid), bluetoothCacheMode);

                    if (result_charact.Status != GattCommunicationStatus.Success)
                    {
                        FatalError("Failed to get Scale service characteristics 0x2A80 " + result_charact.Status.ToString());
                        return;
                    }

                    if (result_charact.Characteristics.Count != 1)
                    {
                        FatalError("Error, expected to find one Scale service characteristics 0x2A80");
                        return;
                    }

                    characteristicScale = result_charact.Characteristics[0];

                    characteristicScale.ValueChanged += CharacteristicScale_ValueChanged;

                    // enable notifications
                    var result = await characteristicScale.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    subscribedForNotificationsScale = true;

                    WriteAppIdentity(); // in order to start receiving weights

                    statusScale = StatusEnum.CharacteristicConnected;

                    message_scale = "Connected to Acaia ";

                    PanelConnectDisconnect.Background = new SolidColorBrush(Windows.UI.Colors.Green);

                    BtnBeansWeight.IsEnabled = true;
                    BtnTare.IsEnabled = true;
                    BtnStartLog.IsEnabled = true;
                    BtnStopLog.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    FatalError("Exception when accessing service or its characteristics: " + ex.Message);
                    return;
                }
            }
            else if (statusScale == StatusEnum.CharacteristicConnected)
            {
                WriteHeartBeat();
            }
            else
            {
                FatalError("Unknown Status for Acaia scale" + statusScale.ToString());
                return;
            }



            //  ===========   TESTO  ==================

            if (statusTesto == StatusEnum.Disabled)
            {
                // do nothing
            }
            else if (statusTesto == StatusEnum.Disconnected)
            {
                foreach (var d in KnownDevices)
                {
                    if (d.Name.StartsWith("T549i"))
                    {
                        deviceIdTesto = d.Id;

                        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                        localSettings.Values["DeviceIdTesto"] = deviceIdTesto;

                        statusTesto = StatusEnum.Discovered;

                        device_watcher_needs_stopping = true;

                        message_testo = "Discovered " + deviceIdTesto + " ";
                    }
                }
            }
            else if (statusTesto == StatusEnum.Discovered)
            {
                try
                {
                    if (bluetoothDeviceTesto == null)
                    {
                        try
                        {
                            bluetoothDeviceTesto = await BluetoothLEDevice.FromIdAsync(deviceIdTesto);
                        }
                        catch (Exception) { }
                    }

                    if (bluetoothDeviceTesto == null)
                    {
                        FatalError("Failed to create Testo BluetoothLEDevice");
                        return;
                    }

                    GattDeviceServicesResult result_service = await bluetoothDeviceTesto.GetGattServicesForUuidAsync(
                    new Guid(TestoServiceGuid), bluetoothCacheMode);

                    if (result_service.Status != GattCommunicationStatus.Success)
                    {
                        FatalError("Failed to get Testo service 0xfff0 " + result_service.Status.ToString());
                        return;
                    }

                    if (result_service.Services.Count != 1)
                    {
                        FatalError("Error, expected to find one Testo service 0xfff0");
                        return;
                    }

                    var service = result_service.Services[0];

                    // Ensure we have access to the device.
                    var accessStatus = await service.RequestAccessAsync();

                    if (accessStatus != DeviceAccessStatus.Allowed)
                    {
                        FatalError("Do not have access to the Testo service 0xfff0");
                        return;
                    }

                    // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
                    // and the new Async functions to get the characteristics of unpaired devices as well. 

                    //  ====  NOTIF characteristics  =====

                    GattCharacteristicsResult result_charact = await service.GetCharacteristicsForUuidAsync(new Guid(TestoCharactNotifGuid), bluetoothCacheMode);

                    if (result_charact.Status != GattCommunicationStatus.Success)
                    {
                        FatalError("Failed to get Testo service characteristics 0xfff2 " + result_charact.Status.ToString());
                        return;
                    }

                    if (result_charact.Characteristics.Count != 1)
                    {
                        FatalError("Error, expected to find one Testo service characteristics 0xfff2");
                        return;
                    }

                    characteristicTestoNotif = result_charact.Characteristics[0];

                    characteristicTestoNotif.ValueChanged += CharacteristicTesto_ValueChanged;

                    // enable notifications
                    var result = await characteristicTestoNotif.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    subscribedForNotificationsTesto = true;


                    //  ====  WRITE characteristics  =====

                    result_charact = await service.GetCharacteristicsForUuidAsync(new Guid(TestoCharactWriteGuid), bluetoothCacheMode);

                    if (result_charact.Status != GattCommunicationStatus.Success)
                    {
                        FatalError("Failed to get Testo service characteristics fff1 " + result_charact.Status.ToString());
                        return;
                    }

                    if (result_charact.Characteristics.Count != 1)
                    {
                        FatalError("Error, expected to find one Testo service characteristics fff1");
                        return;
                    }

                    characteristicTestoWrite = result_charact.Characteristics[0];



                    WriteCommandsToEnablePressureMeasurements(); // in order to start receiving pressure

                    statusTesto = StatusEnum.CharacteristicConnected;

                    message_testo = "Connected to T549i ";

                    PanelConnectDisconnect.Background = new SolidColorBrush(Windows.UI.Colors.Green);

                    BtnBeansWeight.IsEnabled = true;
                    BtnTare.IsEnabled = true;
                    BtnStartLog.IsEnabled = true;
                    BtnStopLog.IsEnabled = false;

                    if(ChkTesto.IsOn)
                        BtnZeroPressure.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    FatalError("Exception when accessing service or its characteristics: " + ex.Message);
                    return;
                }
            }
            else if (statusTesto == StatusEnum.CharacteristicConnected)
            {
                // do nothing
            }
            else
            {
                FatalError("Unknown Status for Testo" + statusTesto.ToString());
                return;
            }

            // Do not need device watcher anymore
            if (statusScale != StatusEnum.Disconnected && statusTesto != StatusEnum.Disconnected && device_watcher_needs_stopping)
                StopBleDeviceWatcher();

            // Notify
            if (message_scale != "" || message_testo != "")
                NotifyUser(message_scale + message_testo, NotifyType.StatusMessage);


            heartBeatTimer.Start();
        }

        private async void Disconnect()
        {
            heartBeatTimer.Stop();

            StopBleDeviceWatcher();

            if (subscribedForNotificationsScale)
            {
                await characteristicScale.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);

                characteristicScale.ValueChanged -= CharacteristicScale_ValueChanged;

                subscribedForNotificationsScale = false;
            }

            if (subscribedForNotificationsTesto)
            {
                await characteristicTestoNotif.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);

                characteristicTestoNotif.ValueChanged -= CharacteristicTesto_ValueChanged;

                subscribedForNotificationsTesto = false;
            }

            bluetoothDeviceScale?.Dispose();
            bluetoothDeviceScale = null;

            BtnConnect.IsEnabled = true;
            BtnDisconnect.IsEnabled = false;

            BtnBeansWeight.IsEnabled = false;
            BtnTare.IsEnabled = false;
            BtnStartLog.IsEnabled = false;
            BtnStopLog.IsEnabled = false;
            BtnZeroPressure.IsEnabled = false;

            statusScale = StatusEnum.Disconnected;

            statusTesto = ChkTesto.IsOn ? StatusEnum.Disconnected : StatusEnum.Disabled;

            LogBrewWeight.Text = "---";
            LogBrewTime.Text = "---";
            LogBrewPressure.Text = "---";

            PanelConnectDisconnect.Background = new SolidColorBrush(Windows.UI.Colors.Yellow);

            ScenarioControl.SelectedIndex = 0;
        }

        private void CharacteristicScale_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] data;
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out data);

            // for debug
            //var message = "ValueChanged at " + DateTime.Now.ToString("hh:mm:ss.FFF ") + BitConverter.ToString(data);
            //NotifyUser(message, NotifyType.StatusMessage);

            double weight_gramm = 0.0;
            bool is_stable = true;
            if(DecodeWeight(data, ref weight_gramm, ref is_stable))
                NotifyWeight(weight_gramm);
        }

        private void CharacteristicTesto_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] data;
            CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out data);

            // for debug
            //var message = "ValueChanged at " + DateTime.Now.ToString("hh:mm:ss.FFF ") + BitConverter.ToString(data);
            //NotifyUser(message, NotifyType.StatusMessage);

            double pressure_bar = 0.0;
            if (DecodePressure(data, ref pressure_bar))
                NotifyPressure(pressure_bar);
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            NotifyUser("Disconnected", NotifyType.StatusMessage);
            Disconnect();
        }

        private void BtnTare_Click(object sender, RoutedEventArgs e)
        {
            WriteTare();
            LogBrewTime.Text = "---";
            NotifyUser("Tare", NotifyType.StatusMessage);
        }

        private void BtnBeansWeight_Click(object sender, RoutedEventArgs e)
        {
            DetailBeansWeight.Text = LogBrewWeight.Text;
            NotifyUser("Bean weight saved", NotifyType.StatusMessage);
        }

        private void BtnStartLog_Click(object sender, RoutedEventArgs e)
        {
            BtnBeansWeight.IsEnabled = false;
            BtnTare.IsEnabled = false;
            BtnStartLog.IsEnabled = false;
            BtnStopLog.IsEnabled = true;

            if(LogBrewWeight.Text != "0.0")
                WriteTare(); // tare, as I always forget to do this

            startTimeWeight = DateTime.Now;
            weightEverySec.Start();
            pressureEverySec.Start();

            NotifyUser("Started ...", NotifyType.StatusMessage);
        }

        private void BtnStopLog_Click(object sender, RoutedEventArgs e)
        {
            BtnBeansWeight.IsEnabled = true;
            BtnTare.IsEnabled = true;
            BtnStartLog.IsEnabled = true;
            BtnStopLog.IsEnabled = false;

            startTimeWeight = DateTime.MinValue;

            weightEverySec.Stop(0);
            pressureEverySec.Stop(weightEverySec.GetActualNumValues());

            DetailDateTime.Text = DateTime.Now.ToString("yyyy MMM dd ddd HH:mm");
            DetailCoffeeWeight.Text = LogBrewWeight.Text;
            DetailTime.Text = weightEverySec.GetActualTimingString();
            DetailCoffeeRatio.Text = GetRatioString();

            // switch to brew details page
            BtnSaveLog.IsEnabled = true;
            ScenarioControl.SelectedIndex = 1;

            NotifyUser("Stopped", NotifyType.StatusMessage);
        }

        private static bool IsCtrlKeyPressed()
        {
            var ctrlState = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control);
            return (ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }

        private async void Grid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            string help_message = "Shortcuts\r\nF1\tHelp\r\nCtrl-C\tConnect\r\nCtrl-D\tDisconnect\r\n";
            help_message += "Ctrl-B\tBeans weight\r\nCtrl-T\tTare\r\nCtrl-S\tStart / Stop\r\n";
            help_message += "Ctrl-Up\tGrind +\r\nCtrl-Dn\tGrind -\r\n\r\nCtrl-A\tAdd to log\r\n";
            help_message += "Ctrl-1\tMenu item 1, etc";

            if (IsCtrlKeyPressed())
            {
                switch (e.Key)
                {
                    case VirtualKey.C:
                        if (BtnConnect.IsEnabled)
                            BtnConnect_Click(null, null);
                        break;

                    case VirtualKey.D:
                        if (BtnDisconnect.IsEnabled)
                            BtnDisconnect_Click(null, null);
                        break;

                    case VirtualKey.B:
                        if (BtnBeansWeight.IsEnabled)
                            BtnBeansWeight_Click(null, null);
                        break;

                    case VirtualKey.T:
                        if (BtnTare.IsEnabled)
                            BtnTare_Click(null, null);
                        break;

                    case VirtualKey.S:
                        if (BtnStartLog.IsEnabled)
                            BtnStartLog_Click(null, null);
                        else if (BtnStopLog.IsEnabled)
                            BtnStopLog_Click(null, null);
                        break;

                    case VirtualKey.Down:
                        BtnGrindMinus_Click(null, null);
                        break;
                    case VirtualKey.Up:
                        BtnGrindPlus_Click(null, null);
                        break;

                    case VirtualKey.A:
                        if (BtnSaveLog.IsEnabled)
                            BtnSaveLog_Click(null, null);
                        break;

                    case VirtualKey.Number1:
                        ScenarioControl.SelectedIndex = 0;
                        break;
                    case VirtualKey.Number2:
                        ScenarioControl.SelectedIndex = 1;
                        break;
                    case VirtualKey.Number3:
                        ScenarioControl.SelectedIndex = 2;
                        break;
                }

                // StatusLabel.Text = DateTime.Now.ToString("mm:ss") + " -- " + e.Key.ToString(); // enable to check if app received key events
                if (ToggleButton.FocusState == FocusState.Unfocused)
                    ToggleButton.Focus(FocusState.Keyboard);
            }
            else
            {
                switch (e.Key)
                {
                    case VirtualKey.F1:
                        var messageDialog = new MessageDialog(help_message);
                        await messageDialog.ShowAsync();
                        break;
                }
            }
        }

        private void BtnGrindMinus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grind = Convert.ToDouble(DetailGrind.Text);
                grind -= 0.25;
                DetailGrind.Text = grind.ToString("0.00");
            }
            catch (Exception) { }
        }

        private void BtnGrindPlus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grind = Convert.ToDouble(DetailGrind.Text);
                grind += 0.25;
                DetailGrind.Text = grind.ToString("0.00");
            }
            catch (Exception) { }
        }

        private void ChkTesto_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["EnableTesto"] = ChkTesto.IsOn ? "true" : "false";
        }

        private void BtnZeroPressure_Click(object sender, RoutedEventArgs e)
        {
            WriteZeroPressure();
            NotifyUser("Zero pressure sensor", NotifyType.StatusMessage);
        }
    }

    public enum NotifyType { StatusMessage, ErrorMessage };
}
