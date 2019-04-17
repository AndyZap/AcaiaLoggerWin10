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
        private string AcaiaDeviceId = String.Empty;

        private BluetoothCacheMode bluetoothCacheMode = BluetoothCacheMode.Cached;

        private BluetoothLEDevice bluetoothDeviceScale = null;
        private GattCharacteristic selectedCharacteristicScale = null;

        private DispatcherTimer heartBeatTimer;

        private enum AppStatusEnum { Disconnected, ScaleDiscovered, CharacteristicConnected }

        private AppStatusEnum appStatus = AppStatusEnum.Disconnected;
        private bool subscribedForNotificationsScale = false;

        public MainPage()
        {
            this.InitializeComponent();

            heartBeatTimer = new DispatcherTimer();
            heartBeatTimer.Tick += dispatcherTimer_Tick;
            heartBeatTimer.Interval = new TimeSpan(0, 0, 3);

            NotifyUser("", NotifyType.StatusMessage);

            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            var val = localSettings.Values["AcaiaDeviceId"] as string;
            AcaiaDeviceId = val == null? "" : val;

            val = localSettings.Values["DetailBeansName"] as string;
            DetailBeansName.Text = val == null ? "" : val;

            val = localSettings.Values["DetailGrind"] as string;
            DetailGrind.Text = val == null ? "" : val;
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

        private void MenuToggleButton_Click(object sender, RoutedEventArgs e)
        {
            Splitter.IsPaneOpen = !Splitter.IsPaneOpen;
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            BtnConnect.IsEnabled = false;
            BtnDisconnect.IsEnabled = true;

            appStatus = AppStatusEnum.Disconnected;

            if (AcaiaDeviceId != String.Empty) // try to connect if we already know the DeviceID
            {
                try
                {
                    bluetoothDeviceScale = await BluetoothLEDevice.FromIdAsync(AcaiaDeviceId);
                }
                catch (Exception) { }
            }
            
            if (bluetoothDeviceScale == null) // Failed to connect with the device ID, need to search for the scale
            {
                if (deviceWatcher == null)
                    StartBleDeviceWatcher();

                NotifyUser("Device watcher started", NotifyType.StatusMessage);
            }
            else // we have bluetoothLeDevice, connect to the characteristic
            {
                appStatus = AppStatusEnum.ScaleDiscovered;
            }
            heartBeatTimer.Start();
        }

        async void dispatcherTimer_Tick(object sender, object e)
        {
            heartBeatTimer.Stop();

            if (appStatus == AppStatusEnum.Disconnected)
            {
                foreach (var d in KnownDevices)
                {
                    if (d.Name.StartsWith("PROCH") || d.Name.StartsWith("ACAIA"))
                    {
                        AcaiaDeviceId = d.Id;

                        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
                        localSettings.Values["AcaiaDeviceId"] = AcaiaDeviceId;

                        StopBleDeviceWatcher();
                        NotifyUser("Found Acaia scale with id " + AcaiaDeviceId, NotifyType.StatusMessage);

                        appStatus = AppStatusEnum.ScaleDiscovered;
                    }
                }
                heartBeatTimer.Start();
            }
            else if (appStatus == AppStatusEnum.ScaleDiscovered)
            {
                NotifyUser("Enabling weight measurements ...", NotifyType.StatusMessage);

                try
                {
                    if (bluetoothDeviceScale == null)
                    {
                        try
                        {
                            bluetoothDeviceScale = await BluetoothLEDevice.FromIdAsync(AcaiaDeviceId);
                        }
                        catch (Exception) { }
                    }

                    if (bluetoothDeviceScale == null)
                    {
                        FatalError("Failed to create BluetoothLEDevice");
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

                    selectedCharacteristicScale = result_charact.Characteristics[0];

                    selectedCharacteristicScale.ValueChanged += CharacteristicScale_ValueChanged;

                    // enable notifications
                    var result = await selectedCharacteristicScale.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    subscribedForNotificationsScale = true;

                    WriteAppIdentity(); // in order to start receiving weights

                    appStatus = AppStatusEnum.CharacteristicConnected;

                    NotifyUser("Connected, subscribed to weight notifications", NotifyType.StatusMessage);

                    BtnBeansWeight.IsEnabled = true;
                    BtnTare.IsEnabled = true;
                    BtnStartLog.IsEnabled = true;
                    BtnStopLog.IsEnabled = false;

                    heartBeatTimer.Start();
                }
                catch (Exception ex)
                {
                    FatalError("Exception when accessing service or its characteristics: " + ex.Message);
                }
            }
            else if(appStatus == AppStatusEnum.CharacteristicConnected)
            {
                WriteHeartBeat();
                heartBeatTimer.Start();
            }
            else
                FatalError("Unknown appStatus" + appStatus.ToString());
        }

        private async void Disconnect()
        {
            heartBeatTimer.Stop();

            StopBleDeviceWatcher();

            if (subscribedForNotificationsScale)
            {
                // Need to clear the CCCD from the remote device so we stop receiving notifications
                var result = await selectedCharacteristicScale.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);

                //if (result != GattCommunicationStatus.Success)
                //    Log("Was not able to disable notifications");

                selectedCharacteristicScale.ValueChanged -= CharacteristicScale_ValueChanged;

                subscribedForNotificationsScale = false;
            }
            bluetoothDeviceScale?.Dispose();
            bluetoothDeviceScale = null;

            BtnConnect.IsEnabled = true;
            BtnDisconnect.IsEnabled = false;

            BtnBeansWeight.IsEnabled = false;
            BtnTare.IsEnabled = false;
            BtnStartLog.IsEnabled = false;
            BtnStopLog.IsEnabled = false;

            appStatus = AppStatusEnum.Disconnected;

            LogBrewWeight.Text = "---";
            LogBrewTime.Text = "---";
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
            {
                NotifyWeight(weight_gramm);
            }
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

            startTimeWeight = DateTime.Now;
            weightEverySec.Start();

            NotifyUser("Started ...", NotifyType.StatusMessage);
        }

        private void BtnStopLog_Click(object sender, RoutedEventArgs e)
        {
            BtnBeansWeight.IsEnabled = true;
            BtnTare.IsEnabled = true;
            BtnStartLog.IsEnabled = true;
            BtnStopLog.IsEnabled = false;

            startTimeWeight = DateTime.MinValue;

            weightEverySec.Stop();

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
                if (Splitter.FocusState == FocusState.Unfocused)
                    Splitter.Focus(FocusState.Keyboard);
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
    }

    public enum NotifyType { StatusMessage, ErrorMessage };
}
