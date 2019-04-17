using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;


namespace AcaiaLogger
{
    public sealed partial class MainPage : Page
    {
        private string LogFileName = "AcaiaLogger.csv";
        private string LogFileHeader = "date,beanName,beanWeight,coffeeWeight,grind,time,notes,weightEverySec,pressureEverySec";

        private ValuesEverySec weightEverySec = new ValuesEverySec();

        private readonly int __MaxRecordsToSave = 50;  // keep the last 50 records only

        public ObservableCollection<LogEntry> BrewLog { get; } = new ObservableCollection<LogEntry>();

        private string ToCsvFile(string s) // make sure we do not save commas into csv, a quick hack
        {
            return s.Replace(",", " ") + ",";
        }

        private string GetRatioString()
        {
            try
            {
                var ratio = Convert.ToDouble(DetailCoffeeWeight.Text) / Convert.ToDouble(DetailBeansWeight.Text);
                return "ratio " + ratio.ToString("0.00");
            }
            catch (Exception)
            {
                return "-";
            }
        }

        private async void BtnSaveLog_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder storageFolder = ApplicationData.Current.RoamingFolder;
            StorageFile file = await storageFolder.CreateFileAsync(LogFileName, CreationCollisionOption.OpenIfExists);

            // 
            StringBuilder new_record = new StringBuilder();

            new_record.Append(ToCsvFile(DetailDateTime.Text));
            new_record.Append(ToCsvFile(DetailBeansName.Text));
            new_record.Append(ToCsvFile(DetailBeansWeight.Text));
            new_record.Append(ToCsvFile(DetailCoffeeWeight.Text));
            new_record.Append(ToCsvFile(DetailGrind.Text));
            new_record.Append(ToCsvFile(DetailTime.Text));
            new_record.Append(ToCsvFile(DetailNotes.Text));
            new_record.Append(weightEverySec.GetValuesString());

            //
            var lines = await FileIO.ReadLinesAsync(file);

            List<string> new_lines = new List<string>();
            new_lines.Add(LogFileHeader);
            new_lines.Add(new_record.ToString());
            for(int i = 1; i < Math.Min(__MaxRecordsToSave, lines.Count); i++)
                new_lines.Add(lines[i]);

            await FileIO.WriteLinesAsync(file, new_lines);

            // update brewLog list
            BrewLog.Insert(0, new LogEntry(new_record.ToString()));

            // save to settings
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["DetailBeansName"] = DetailBeansName.Text;
            localSettings.Values["DetailGrind"] = DetailGrind.Text;

            NotifyUser("Saved to log " + file.Path, NotifyType.StatusMessage);

            BtnSaveLog.IsEnabled = false;
        }

        private async void LoadLog()
        {
            StorageFolder storageFolder = ApplicationData.Current.RoamingFolder;
            StorageFile file = await storageFolder.CreateFileAsync(LogFileName, CreationCollisionOption.OpenIfExists);

            var lines = await FileIO.ReadLinesAsync(file);

            BrewLog.Clear();
            foreach(var line in lines)
            {
                if (line.StartsWith("date,beanName,beanWeight,")) // header line
                    continue;

                BrewLog.Add(new LogEntry(line));
            }
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

    // class which stores average reading for every second. Assumes the readings arrive faster than 1 per sec
    public class ValuesEverySec  
    {
        List<double> values = new List<double>();
        DateTime startTime = DateTime.MinValue;
        double sum = 0.0;
        int num = 0;

        public ValuesEverySec()
        {
        }
        public void Start()
        {
            values.Clear();
            values.Add(0.0);
            sum = 0.0;
            num = 0;

            startTime = DateTime.Now;
        }
        public void Stop()
        {
            startTime = DateTime.MinValue;

            // prune the weights to remove constant values at the end
            while(values.Count > 2)
            {
                var last = values.Count - 1;
                if (Math.Abs(values[last] - values[last - 1]) < 0.15)
                    values.RemoveAt(last);
                else
                    break;
            }
        }

        public string GetActualTimingString()
        {
            return values.Count.ToString();
        }

        public string GetValuesString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var w in values)
                sb.Append(w.ToString("0.0") + ";");

            var str = sb.ToString();

            return str.Substring(0, str.Length-1);
        }

        public bool NewReading(double w)
        {
            if (startTime == DateTime.MinValue)
                return true;

            var ts = DateTime.Now - startTime;
            if (ts.TotalSeconds > values.Count)
            {
                if (num <= 1)
                    return false; // if no more reading values was accumulated over the previous second, i.e. something is wrong

                values.Add(sum / (double)num);
                sum = 0.0;
                num = 0;
            }

            sum += w;
            num++;

            return true;
        }
    }

    public class LogEntryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            LogEntry s = value as LogEntry;

            string ratio_string = "";
            try
            {
                var ratio = System.Convert.ToDouble(s.coffeeWeight) / System.Convert.ToDouble(s.beanWeight);
                ratio_string = ratio.ToString("0.00");
            }
            catch (Exception) { }


            return  "\t" + s.date.Substring(5) + " \t" + s.beanWeight + " -> " + s.coffeeWeight +
                " in " + s.time + " sec  ratio " + ratio_string + "\t  grind " + s.grind;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return true;
        }
    }
    public class LogNotesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            LogEntry s = value as LogEntry;
            return "\t" + s.notes;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return true;
        }
    }
    public class LogBeanNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            LogEntry s = value as LogEntry;
            return s.beanName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return true;
        }
    }
}