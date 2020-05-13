using System;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace xpra
{
    public class MainWindowViewModel : Observable, IRequestFocus
    {
        #region Properties

        const string HostRegex = @"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$";

        public event EventHandler<FocusRequestedEventArgs> FocusRequested;

        private MainService MainService { get; } = new MainService();

        public bool Loaded { get; set; }
        public bool SkipComboChanged { get; set; }

        private string _version;
        public string Version
        {
            get { return _version; }
            set { _version = value; NotifyPropertyChanged(); }
        }
        
        private Page _currentPage;
        public Page CurrentPage
        {
            get { return _currentPage; }
            set {
                Title = value.ToString();
                _currentPage = value;
                HasBack = _currentPage == Page.Settings || _currentPage == Page.About;
                NotifyPropertyChanged();
                NotifyPropertyChanged("HasBack");
                NotifyPropertyChanged("Title");
            }
        }
        public bool HasBack { get; set; }
        public string Title { get; set; }

        public Connection SelectedConnection { 
            get { return MainService.SelectedConnection; }
            set { MainService.SelectedConnection = value;  }
        }
        public ObservableCollection<Connection> ConnectionList { 
            get { return MainService.ConnectionList; }
        }

        //public bool HasApps
        //{ 
        //    get 
        //    { 
        //        return ConnectionService.Aps != null && ConnectionService.Aps.Count > 0; 
        //    }
        //}

        private ApStatus apStatus;
        public ApStatus ApStatus
        {
            get { return apStatus; }
            set
            {
                apStatus = value;
                NotifyPropertyChanged();
                //NotifyPropertyChanged("ConnectButtonText");
                //NotifyPropertyChanged("ConnectButtonColor");
            }
        }

        private ConnectStatus _connectStatus;
        public ConnectStatus ConnectStatus
        {
            get { return _connectStatus; }
            set
            {
                _connectStatus = value;
                NotifyPropertyChanged();
                //NotifyPropertyChanged("MessageColor");
                //NotifyPropertyChanged("ConnectButtonIsEnabled");

            }
        }

        private bool _isWorking;
        public bool IsWorking
        {
            get { return _isWorking; }
            set
            {
                _isWorking = value;
                NotifyPropertyChanged();
            }
        }
        
        //public string ConnectButtonText => 
        //    (DriveStatus == DriveStatus.CONNECTED 
        //    || DriveStatus == DriveStatus.BROKEN ) ? "Disconnect" : "Connect";
        //public string ConnectButtonColor => DriveStatus == DriveStatus.CONNECTED ? "#689F38" : "#607d8b";
        //public bool ConnectButtonIsEnabled => true;
        //public bool IsSettingsChanged { get; set; }

        private string message;
        public string Message
        {
            get { return message; }
            set { message = value; NotifyPropertyChanged(); }
        }
        public Brush MessageColor
        {
            get
            {
                return Brushes.Black;
                //return MountStatus == MountStatus.OK ? Brushes.Black : Brushes.Red;
            }
        }
        private string password;
        public string Password
        {
            get { return password; }
            set { password = value; NotifyPropertyChanged(); }
        }
        
        
        #endregion

        #region Constructor

        public MainWindowViewModel(ReturnBox rb)
        {
            var name = "PATH";
            var scope = EnvironmentVariableTarget.User; // or User
            var oldValue = Environment.GetEnvironmentVariable(name, scope);
            var newValue = @"C:\Xpra-Client-Python3-x86_64_4.0-r26306;" + oldValue;
            Environment.SetEnvironmentVariable(name, newValue, scope);

            // Monitor
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 5);
            dispatcherTimer.Start();

            CurrentPage = Page.Main;
            LoadApsAsync();
            GetVersionsAsync();
            if (rb != null)
                Message = rb.Error;           
        }

        private async void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            await Task.Run(() =>
            {
                var apsServer = MainService.GetApsServer();
                var apsLocal = MainService.GetApsLocal();
                
            });
            foreach (var ap in MainService.SelectedConnection.ApList)
                ap.Status = ApStatus.IDLE;
        }

        #endregion

        #region Async Methods

        private void WorkStart(string message)
        {
            Message = message;
            if (IsWorking)
                return;
            IsWorking = true;
        }
        private void WorkDone(ReturnBox r = null)
        {
            IsWorking = false;
            
            if (r == null)
            {
                Message = "";
                return;
            }
            ApStatus = r.ApStatus;
            ConnectStatus = r.ConnectStatus;
            Message = r.Error;

            switch (r.ConnectStatus)
            {
                case ConnectStatus.BAD_HOST:
                    CurrentPage = Page.Host;
                    OnFocusRequested("Host");
                    break;
                case ConnectStatus.BAD_PASSWORD:
                case ConnectStatus.BAD_KEY:
                    CurrentPage = Page.Password;
                    OnFocusRequested(nameof(Password));
                    break;
                case ConnectStatus.OK:
                    CurrentPage = Page.Main;
                    Message = r.ApStatus.ToString();
                    NotifyPropertyChanged("HasDrives");
                    break;
                default:
                    break;
            }
            IsWorking = false;

        }

        public async void LoadApsAsync()
        {
            Loaded = false;
            WorkStart("Loading apps...");
            Settings settings = null;
            await Task.Run(() =>
            {
                settings = MainService.LoadSettings();                
            });

            MainService.UpdateFromSettings(settings);
            //UpdateObservableAps();

            if (MainService.ConnectionList.Count == 0)
            {
                CurrentPage = Page.Host;
                OnFocusRequested("Host");
                WorkDone();
            }
            else
            {
                ConnectAsync();
                CheckApStatusAsync();
            }
            Loaded = true;

        }

        //private void UpdateObservableAps()
        //{
        //    ConnectionList.Clear();
        //    MainService.ConnectionList.ForEach(ConnectionList.Add);            
        //    NotifyPropertyChanged("Aps");
        //}

        void ReportStatus(string message)
        {
            Message = message;
        }

        private async void ConnectAsync()
        {
            WorkStart("Connecting...");
            var status = new Progress<string>(ReportStatus);
            ReturnBox r = await Task.Run(() => MainService.Connect(SelectedConnection, status));
            WorkDone(r);
        }

        private async void CheckApStatusAsync()
        {
            //if (SelectedAp != null)
            //{
            //    WorkStart("Checking status...");
            //    ReturnBox r = await Task.Run(() => ConnectionService.CheckDriveStatus(SelectedAp));
            //    WorkDone(r);
            //}
        }

        private async void GetVersionsAsync()
        {
            Version = await Task.Run(() => MainService.GetVersions());
        }

        #endregion

        #region Command methods


        private async void OnConnect(object obj)
        {
            if (IsWorking)
                return;

            Message = "";

            if (SelectedConnection == null 
                || SelectedConnection.ApList.Count == 0
                || string.IsNullOrEmpty(SelectedConnection.Host))
            {
                //IsDriveNew = true;
                //SelectedAp = FreeDriveList.First();
                CurrentPage = Page.Host;
                return;
            }
            ConnectAsync();
            
        }

        private void OnConnectHost(object obj)
        {
            if (string.IsNullOrEmpty(SelectedConnection.Host))
            {
                Message = "Server is required";
                OnFocusRequested(nameof(SelectedConnection.Host));
                return;
            }
            ConnectAsync();
        }

        private async void OnConnectPassword(object obj)
        {           
            WorkStart("Connecting...");
            var status = new Progress<string>(ReportStatus);
            ReturnBox r = await Task.Run(() => MainService.ConnectPassword(SelectedConnection, password, status));
            //SkipComboChanged = true;
            //UpdateObservableAps();
            //SkipComboChanged = false;
            WorkDone(r);
        }
        
        private void OnSettingsShow(object obj)
        {
            //IsDriveNew = false;
            //if(GoldDriveList.Count == 0)
            //{
            //    IsDriveNew = true;
            //}
        }
        private async void OnSettingsSave(object obj)
        {
            
            if (string.IsNullOrEmpty(SelectedConnection.Host))
            {
                Message = "Server is required";
                OnFocusRequested("SelectedAp.Host");
                return;
            }

            Regex hostRegex = new Regex(HostRegex);
            if (!hostRegex.Match(SelectedConnection.Host).Success 
                && !hostRegex.Match(SelectedConnection.Host).Success)
            {
                Message = "Invalid server name";
                OnFocusRequested("SelectedAp.Host");
                return;
            }
            await Task.Run(() =>
            {
                Settings settings = MainService.LoadSettings();
                //settings.AddDrive(SelectedAp);
                MainService.SaveSettings(settings);
                //ConnectionService.UpdateDrives(settings);
            });

            //UpdateObservableAps();
            Message = "";
            //IsDriveNew = false;
            //IsDriveEdit = false;

        }
        private void OnSettingsCancel(object obj)
        {
            
            //SelectedAp.Clone(OriginalDrive);
            //Message = "";
            //IsDriveNew = false;
            //IsDriveEdit = false;            
        }

        private void OnSettingsNew(object obj)
        {
            //OriginalDrive = new Ap(SelectedAp);
            //SelectedAp = FreeDriveList.First();
            //IsDriveNew = true;            
        }
        
        private async void OnSettingsDelete(object obj)
        {
           

            //Ap d = SelectedAp;
            //if (GoldDriveList.Contains(d))
            //    GoldDriveList.Remove(d);
            //await Task.Run(() =>
            //{
            //    if (d.Status == DriveStatus.CONNECTED)
            //        ConnectionService.Unmount(d);
            //    Settings settings = ConnectionService.LoadSettings();
            //    settings.AddDrives(GoldDriveList);
            //    ConnectionService.SaveSettings(settings);
            //    ConnectionService.UpdateDrives(settings);
            //});
            //UpdateObservableDrives();
            //if(GoldDriveList.Count == 0)
            //    IsDriveNew = true;
    
        }
        private void OnSettingsEdit(object obj)
        {
            //OriginalDrive = new Ap(SelectedAp);
            //IsDriveEdit = true;
        }
        public void Closing(object obj)
        {
            //Settings settings = ConnectionService.LoadSettings();
            //if (Aps != null)
            //{
            //    //settings.Selected = SelectedAp != null ? SelectedAp.Name : "";
            //    //settings.AddApps(Aps.ToList());
            //    //ConnectionService.SaveSettings(settings);
            //}
        }

        private async void OnRunApp(string appname)
        {


            //ConnectionService.Detach(102);

            WorkStart($"Running {appname}...");
            var status = new Progress<string>(ReportStatus);
            ReturnBox r = await Task.Run(() => MainService.RunAp(SelectedConnection, appname, status));
            WorkDone(r);
        }


        #endregion

        #region Commands

        private ICommand _connectCommand;
        public ICommand ConnectCommand
        {
            get
            {
                return _connectCommand ??
                    (_connectCommand = new RelayCommand(OnConnect));
            }
        }
        private ICommand _connectHostCommand;
        public ICommand ConnectHostCommand
        {
            get
            {
                return _connectHostCommand ??
                    (_connectHostCommand = new RelayCommand(OnConnectHost));
            }
        }
        private ICommand _showPageCommand;
        public ICommand ShowPageCommand
        {
            get
            {
                return _showPageCommand ??
                    (_showPageCommand = new RelayCommand(
                        x =>
                        {
                            Message = "";                            
                            CurrentPage = (Page)x;
                            
                            if (CurrentPage == Page.Settings)
                            {
                                OnSettingsShow(x);
                            }
                            if (CurrentPage == Page.Main)
                            {
                                CheckApStatusAsync();
                            }
                        },
                        // can execute
                        x =>
                        {
                            return CurrentPage != (Page)x; 
                        }));
            }
        }
        
        private ICommand _connectPasswordCommand;
        public ICommand ConnectPasswordCommand
        {
            get
            {
                return _connectPasswordCommand ??
                    (_connectPasswordCommand = new RelayCommand(OnConnectPassword));
            }
        }
        private ICommand _showPasswordCommand;
        public ICommand ShowLoginCommand
        {
            get
            {
                return _showPasswordCommand ??
                    (_showPasswordCommand = new RelayCommand(
                        x => { CurrentPage = Page.Password; }));
            }
        }

        private ICommand _settingsOkCommand;
        public ICommand SettingsOkCommand
        {
            get
            {
                return _settingsOkCommand ?? (_settingsOkCommand = new RelayCommand(
                   // action
                   x =>
                   {
                       OnSettingsSave(x);
                   },
                   // can execute
                   x =>
                   {
                       return true; // IsSettingsChanged;
                   }));
            }
        }
        private ICommand _settingsNewCommand;
        public ICommand SettingsNewCommand
        {
            get
            {
                return _settingsNewCommand ??
                    (_settingsNewCommand = new RelayCommand(OnSettingsNew));
            }
        }
        private ICommand _settingsCancelCommand;
        public ICommand SettingsCancelCommand
        {
            get
            {
                return _settingsCancelCommand ??
                    (_settingsCancelCommand = new RelayCommand(OnSettingsCancel));
            }
        }
        private ICommand _settingsDeleteCommand;
        public ICommand SettingsDeleteCommand
        {
            get
            {
                return _settingsDeleteCommand ?? (_settingsDeleteCommand = new RelayCommand(
                   // action
                   x =>
                   {
                       OnSettingsDelete(x);
                   },
                   // can execute
                   x =>
                   {
                       return true;
                       //return GoldDriveList != null && GoldDriveList.Count > 0;
                   }));
            }
        }
        private ICommand _settingsEditCommand;
        public ICommand SettingsEditCommand
        {
            get
            {
                return _settingsEditCommand ?? (_settingsEditCommand = new RelayCommand(
                   // action
                   x =>
                   {
                       OnSettingsEdit(x);
                   },
                   // can execute
                   x =>
                   {
                       return true;
                       //return GoldDriveList != null && GoldDriveList.Count > 0;
                   }));
            }
        }
        private ICommand _githubCommand;
        public ICommand GithubCommand
        {
            get
            {
                return _githubCommand ??
                    (_githubCommand = new RelayCommand(
                        url => System.Diagnostics.Process.Start(url.ToString())));
            }
        }
        private ICommand _runTerminalCommand;
        public ICommand RunTerminalCommand
        {
            get
            {
                return _runTerminalCommand ??
                    (_runTerminalCommand = new RelayCommand(
                        url => System.Diagnostics.Process.Start("cmd.exe")));
            }
        }
        private ICommand _openLogsFolderCommand;
        public ICommand OpenLogsFolderCommand
        {
            get
            {
                return _openLogsFolderCommand ??
                    (_openLogsFolderCommand = new RelayCommand(
                        url => System.Diagnostics.Process.Start("explorer.exe", MainService.LocalAppData)));
            }
        }


        private ICommand _runApCommand;
        public ICommand RunApCommand
        {
            get
            {
                return _runApCommand ??
                   (_runApCommand = new RelayCommand(
                       x =>
                       {
                           var appname = x.ToString();
                           OnRunApp(appname);
                       },
                       // can execute
                       x =>
                       {
                           return true;
                       }));
            }
        }

        #endregion

        #region events

        protected virtual void OnFocusRequested(string propertyName)
        {
            FocusRequested?.Invoke(this, new FocusRequestedEventArgs(propertyName));
        }
        public void OnComboChanged()
        {
            if (!Loaded)
                return;
            if (SkipComboChanged)
                return;
            if (CurrentPage == Page.Settings)
                return;

           // CheckDriveStatusAsync();
        }


        #endregion

    }
}

