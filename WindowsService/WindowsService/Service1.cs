using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using System.Xml;
using Crc32C;

namespace WindowsService {

    public enum Status { Failed, Success, None };

    public class DownloadFile
    {
        public string Url { get; set; }
        public string Name { get; set; }
        public Status Status { get; set; }
        public uint Crc { get; set; }
        public DateTime LastAccess { get; set; }

        public DownloadFile(string url,string name)
        {
            Url = url;
            Name = name;
            Status = Status.None;
        }
    }

    public partial class Service1 : ServiceBase {
        private readonly Timer _timer = new Timer();
        private readonly List<DownloadFile> _files = new List<DownloadFile>();
        private static log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static int _interval = 0;
        public enum SimpleServiceCustomCommands { ChangingConfig = 128,  ChangingUrls= 129};
        private static PerformanceCounter performanceCounter,performanceCounter2;

        public Service1() {
            InitializeComponent();
        }

        protected override void OnStart(string[] args) {
            ReadConfigurationForLog4Net(AppDomain.CurrentDomain.BaseDirectory + "log4net.config");
            log.Info("Serwis rozpoczal swoje dzialanie.");
            GetUrls(AppDomain.CurrentDomain.BaseDirectory + "\\urls.txt");
            GetConfig(AppDomain.CurrentDomain.BaseDirectory + "\\config.txt");
            CreateCounters();
            _timer.Elapsed += Process;
            _timer.Enabled = true;
        }

        private void ReadConfigurationForLog4Net(string path) {
            var log4NetConfig = new XmlDocument();
            try {
                log4NetConfig.Load(File.OpenRead(path));
                var repo = log4net.LogManager.CreateRepository(Assembly.GetEntryAssembly(),typeof(log4net.Repository.Hierarchy.Hierarchy));
                log4net.Config.XmlConfigurator.Configure(repo, log4NetConfig["log4net"]);
                log.Debug("Wczytano konfiguracje Log4Net.");
            }
            catch(Exception e) {
                log.Error("Blad ladowania konfiguracji " + e.Message);
            }
        }

        private void GetConfig(string path)
        {
            if (File.Exists(path)) {
                try {
                    using (var sr = new StreamReader(path)) {
                        _interval = int.Parse(sr.ReadLine());
                    }
                    _timer.Interval = _interval;
                    log.Debug("Wczytano config dla serwisu.");
                }
                catch (IOException e) {
                    log.Error(e.Message);
                }
            }
            else
                log.Fatal(path + " nie istnieje.");
        }

        private void GetUrls(string path)
        {
            if (File.Exists(path)) {
                try {
                    _files.Clear();
                    using (var sr = new StreamReader(path)) {
                        while (sr.Peek() >= 0)
                        {
                            var line = sr.ReadLine().Split(' ');
                            _files.Add(new DownloadFile(line[0],line[1]));
                        }
                    }
                    log.Debug("Wczytano ulrs dla serwisu.");
                }
                catch (IOException e) {
                    log.Error(e.Message);
                }
            }
            else
                log.Fatal(path + " nie istnieje.");
        }

        private static void CreateCounters()
        {
            if (!PerformanceCounterCategory.Exists("Projekt PLA4"))
            {
                string counterName = "Pobrane bajty";
                string firstCounterHelp = "Monitorowanie pobranych bajtów";
                string categoryHelp = "Statystyki dla serwisu PLA4";

                PerformanceCounterCategory customCategory = new PerformanceCounterCategory("Projekt PLA4", counterName);
                PerformanceCounterCategory.Create("Projekt PLA4", categoryHelp, PerformanceCounterCategoryType.SingleInstance, counterName, firstCounterHelp);
            }
            log.Debug("Utworzono licznik wydajnosci.");
            performanceCounter = new PerformanceCounter("Projekt PLA4", "Pobrane bajty", false);
            performanceCounter.RawValue = 0;
        }

        protected override void OnStop() {
            log.Info("Serwis zakonczyl swoje dzialanie.");
            Dispose(true);
        }

        private async void GetResourceAsync(DownloadFile file) {
            try
            {
                log.Debug("zaczynam pobierac " + file.Name);
                using (var client = new WebClient())
                {
                    var responseBody = await client.DownloadStringTaskAsync(file.Url);
                    byte[] bytes = Encoding.ASCII.GetBytes(responseBody);
                    performanceCounter.IncrementBy(bytes.Length);
                    uint crc = Crc32CAlgorithm.Compute(bytes);
                    log.Debug(file.Name + " zasob pobrany pomyslnie.");

                    if (file.Status == Status.Success)
                    {
                        if (crc != file.Crc) { 
                            log.Info(file.Name + " rozni sie od poprzednio pobranego.");
                        }
                    }
                    file.Status = Status.Success;
                    file.Crc = crc;
                    file.LastAccess = DateTime.Now;
                }
            }
            catch (WebException e)
            {
                file.Status = Status.Failed;
                log.Error(file.Name + " cos poszlo nie tak " + e.Message);
            }
        }

        private void Process(object source, ElapsedEventArgs e) {
            try
            {
                log.Debug("Rozpoczeto pobieranie zasobow");
                foreach (var file in _files)
                    GetResourceAsync(file);
            }
            catch
            {
                log.Error("Blad procesowania.");
            }
        }

        protected override void OnCustomCommand(int command)
        {
            base.OnCustomCommand(command);

            switch (command)
            {
                case (int)SimpleServiceCustomCommands.ChangingConfig:
                    GetConfig(AppDomain.CurrentDomain.BaseDirectory + "\\config.txt");
                    break;

                case (int)SimpleServiceCustomCommands.ChangingUrls:
                    GetUrls(AppDomain.CurrentDomain.BaseDirectory + "\\urls.txt");
                    break;
                default:
                    break;
            }
        }

    }
}