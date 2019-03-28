using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;

namespace AcaiaLogger
{
    public sealed partial class MainPage : Page
    {
        public List<LogEntry> BrewLog = new List<LogEntry>();

        private string LogFileName = "AcaiaLogger.csv";

        private async void BtnSaveLog_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder storageFolder = ApplicationData.Current.RoamingFolder;
            StorageFile file = await storageFolder.CreateFileAsync(LogFileName, CreationCollisionOption.OpenIfExists);




            await FileIO.AppendTextAsync(file, "Example of writing a string\r\n");

            NotifyUser("Saved to log " + file.Path, NotifyType.StatusMessage);
        }

        private async void LoadLog()
        {
            StorageFolder storageFolder = ApplicationData.Current.RoamingFolder;
            StorageFile file = await storageFolder.CreateFileAsync(LogFileName, CreationCollisionOption.OpenIfExists);




            await FileIO.AppendTextAsync(file, "Example of writing a string\r\n");

            NotifyUser("Saved to log " + file.Path, NotifyType.StatusMessage);
        }
    }

    public class LogEntry
    {
        public string date;
        public string beanName;
        public string beanWeight;
        public string coffeeWeight;
        public string grind;
        public string time;
        public string notes;
        public List<double> flow = new List<double>();

        public LogEntry(string csv_file_line)
        {
            var words = csv_file_line.Split(',');

            date = words[0];
            beanName = words[1];
            beanWeight = words[2];
            coffeeWeight = words[3];
            grind = words[4];
            time = words[5];
            notes = words[6];
        }
    }

    public class ScenarioBindingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            LogEntry s = value as LogEntry;
            return s.date + " " + s.beanWeight + " -> " + s.coffeeWeight +
                " in " + s.time + " sec, grind " + s.grind + " " + s.beanName + " " + s.notes;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return true;
        }
    }
}