using System;
using System.ServiceProcess;
using System.Threading;
using System.Windows;
using System.ComponentModel;
using System.IO;
using System.Collections.ObjectModel;

namespace Wpf
{
    public class Url
    {
        public string Uri { get; set; }
        public string Name { get; set; }
        public Url(string uri,string name)
        {
            Uri = uri;
            Name = name;
        }
    }

    public class View : INotifyPropertyChanged
    {
        public ObservableCollection<Url> Urls { get; set; }
        public ObservableCollection<string> Logs { get; set; }
        public string Info { get; set; }
        public string Status { get; set; }
        public int Interval { get; set; }

        public View()
        {
            Info = Status = "";
            Interval = 0;
            Urls = new ObservableCollection<Url>();
            Logs = new ObservableCollection<string>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class MainWindow : Window
    {
        bool connection = false;
        ServiceController sc = null;
        private readonly View _view;
        private readonly string serviceName = "Service1";
        private readonly string path = "../../../WindowsService/bin/Debug/";

        public MainWindow()
        {
            InitializeComponent();
            _view = new View();
            DataContext = _view;
            Resources.ItemsSource = _view.Urls;
            Logi.ItemsSource = _view.Logs;
            GetConfig(path + "config.txt");
            GetUrls(path + "urls.txt");
            ServiceController[] scServices;
            scServices = ServiceController.GetServices();

            foreach (ServiceController scTemp in scServices)
            {
                if (scTemp.ServiceName == serviceName)
                {
                    sc = scTemp;
                    connection = true;
                    UpdateInfoService($"Połączono z serwisem {serviceName}");
                    UpdateStatusService();
                }
            }
            if (!connection) UpdateInfoService($"Serwis {serviceName} nie istnieje");
        }

        private void UpdateStatusService()
        {
            sc.Refresh();
            _view.Status = sc.Status.ToString();
            _view.OnPropertyChanged("Status");
        }

        private void UpdateInfoService(string text)
        {
            _view.Info = text;
            _view.OnPropertyChanged("Info");
        }

        private void Start_Button_Click(object sender, RoutedEventArgs e)
        {
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                sc.Start();
                while (sc.Status == ServiceControllerStatus.Stopped)
                {
                    Thread.Sleep(1000);
                    sc.Refresh();
                }
                UpdateStatusService();
            }
            else UpdateInfoService("Serwis już jest uruchomiony.");
        }

        private void Stop_Button_Click(object sender, RoutedEventArgs e)
        {
            if (sc.Status == ServiceControllerStatus.Running)
            {
                sc.Stop();
                while (sc.Status == ServiceControllerStatus.Running)
                {
                    Thread.Sleep(1000);
                    sc.Refresh();
                }
                UpdateStatusService();
            }
            else UpdateInfoService("Serwis jest już zatrzymany.");
        }

        private void GetConfig(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    using (var sr = new StreamReader(path))
                    {
                        _view.Interval = int.Parse(sr.ReadLine());
                    }
                }
                catch (IOException e)
                {
                    UpdateInfoService(e.Message);
                }
            }
            else UpdateInfoService("Brak pliku konfiguracyjnego.");
        }

        private void GetUrls(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    using (var sr = new StreamReader(path))
                    {
                        while (sr.Peek() >= 0)
                        {
                            var line = sr.ReadLine().Split(' ');
                            _view.Urls.Add(new Url(line[0], line[1]));
                        }
                    }
                }
                catch (IOException e)
                {
                    UpdateInfoService(e.Message);
                }
            }
            else UpdateInfoService("Brak pliku z linkami.");
        }

        private void GetLogs(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    _view.Logs.Clear();
                    FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using (var sr = new StreamReader(fs))
                    {
                        while (sr.Peek() >= 0)
                        {
                            var line = sr.ReadLine();
                            _view.Logs.Add(line);
                        }
                    }
                }
                catch (IOException e)
                {
                    UpdateInfoService(e.Message);
                }
            }
            else UpdateInfoService("Brak pliku z logami.");
        }

        private void Interval_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int val = int.Parse(Interval.Text);
                StreamWriter sw = new StreamWriter(path + "config.txt");
                sw.Write(val);
                sw.Close();
                sc.ExecuteCommand(128);
            }
            catch (Exception ex)
            {
                UpdateInfoService(ex.Message);
            }
        }

        private void Add_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Url.Text.Length > 1 && Name.Text.Length > 1)
            {
                try
                {
                    File.AppendAllText(path + "urls.txt", "\n" + Url.Text + " " + Name.Text);
                    _view.Urls.Add(new Url(Url.Text, Name.Text));
                    sc.ExecuteCommand(129);
                }
                catch (Exception ex)
                {
                    UpdateInfoService(ex.Message);
                }
            }
            else UpdateInfoService("Link lub nazwa są niepoprawne.");
        }

        private void Remove_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _view.Urls.RemoveAt(Resources.SelectedIndex);
                using (StreamWriter sw = new StreamWriter(path + "urls.txt"))
                {
                    for (int i = 0; i < _view.Urls.Count; i++)
                        if (i == _view.Urls.Count - 1)
                            sw.Write(_view.Urls[i].Uri + " " + _view.Urls[i].Name);
                        else
                            sw.WriteLine(_view.Urls[i].Uri + " " + _view.Urls[i].Name);
                }
                sc.ExecuteCommand(129);
            }
            catch (Exception ex)
            {
                UpdateInfoService(ex.Message);
            }
        }

        private void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            GetLogs(path + "logs.txt");
            for (int i = 0; i < _view.Logs.Count; i++)
                _view.Logs.Move(_view.Logs.Count - 1, i);
        }
    }
}
