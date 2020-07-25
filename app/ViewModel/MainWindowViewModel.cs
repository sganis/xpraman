using System;
using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Windows.Data;
using System.ComponentModel;

namespace xpra
{
    public class MainWindowViewModel : Observable, IRequestFocus
    {
        #region Properties

        static string XPRA = "Xpra-Client-Python3-x86_64_4.0.2-r26625";
        static string XPRA_LOCAL = $@"C:\{XPRA}\Xpra.exe";        
        
        const string HostRegex = @"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$";

        public event EventHandler<FocusRequestedEventArgs> FocusRequested;
        
        private MainService MainService { get; } = new MainService(XPRA_LOCAL);
        public bool IsCheckingStatus { get; set; }
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
            set 
            { 
                MainService.SelectedConnection = value;
                NotifyPropertyChanged();
            }
        }
        public ObservableCollection<Connection> ConnectionList { 
            get { return MainService.ConnectionList; }
            set { 
                MainService.ConnectionList = value;
                NotifyPropertyChanged();
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
        

        private string message;
        public string Message
        {
            get { return message; }
            set { message = value; NotifyPropertyChanged(); }
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
            var newValue = $@"C:\{XPRA};" + oldValue;
            Environment.SetEnvironmentVariable(name, newValue, scope);

            // Monitor
            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 5);
            dispatcherTimer.Start();

            CurrentPage = Page.Main;
            LoadSettingsAsync();
            GetVersionsAsync();

            
            if (rb != null)
                Message = rb.Error;

            Logger.Log("Application started.");
        }
        #endregion

        #region Monitor
        private async void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (IsCheckingStatus)
                return;
            if (SelectedConnection == null)
                return;
            if (!SelectedConnection.IsConnected)
                return;
            List<Ap> apsServer = null;
            List<int> displaysLocal = null;

            IsCheckingStatus = true;
            await Task.Run(() =>
            {
                apsServer = MainService.GetApsServer(SelectedConnection);
                displaysLocal = MainService.GetDisplaysLocal();                
            });

            // loop over current connection apps in settings
            foreach (var display in SelectedConnection.DisplayList)
            {
                if (display.IsWorking)
                    continue;

                var disp_server = apsServer.Where(x => x.DisplayId == display.Id).FirstOrDefault();
                var disp_local = displaysLocal.Where(x => x == display.Id).FirstOrDefault();

                // update display status
                if (disp_server != null)
                {
                    if (disp_local > 0)
                    {
                        // display is attached, status = running
                        display.Status = Status.ACTIVE;
                    }
                    else
                    {
                        display.Status = Status.DETACHED; // detached
                    }
                }
                else
                {
                    display.Status = Status.STOPPED;
                }
                // update apss status
                foreach (var ap in display.ApList)
                {
                    //if (ap.Name.Contains("Subl"))
                    //    ap.ToString();

                    var ap_server = apsServer.Where(x => x.Process == ap.Process && x.DisplayId == display.Id).FirstOrDefault();

                    if (ap_server != null)
                    {
                        // ap is running in server, check local
                        ap.Pgid = ap_server.Pgid;

                        if (disp_local > 0)
                        {
                            // display is attached, status = running
                            ap.Status = Status.ACTIVE;
                            //ap.DisplayStatus = Status.RUNNING;
                        }
                        else
                        {
                            ap.Status = Status.DETACHED;
                            //ap.DisplayStatus = Status.DETACHED;
                        }
                    }
                    else
                    {
                        //ap.DisplayStatus = Status.NOT_RUNNING;
                        ap.Status = Status.STOPPED;
                    }
                }



            }
            IsCheckingStatus = false;
            
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
                    //Message = r.ApStatus.ToString();
                    //NotifyPropertyChanged("HasDrives");
                    break;
                default:
                    break;
            }
            IsWorking = false;

        }

        public async void LoadSettingsAsync()
        {
            Loaded = false;
            WorkStart("Loading apps...");
            Settings settings = null;
            await Task.Run(() =>
            {
                settings = MainService.LoadSettings();                
            });

            MainService.UpdateFromSettings(settings);
            
            if (MainService.ConnectionList.Count == 0)
            {
                CurrentPage = Page.Host;
                OnFocusRequested("Host");
                WorkDone();
            }
            else
            {
                ConnectAsync();
            }
            Loaded = true;

        }

        void ReportStatus(string message)
        {
            Message = message;
        }

        private async void ConnectAsync()
        {
            WorkStart("Connecting...");
            var status = new Progress<string>(ReportStatus);
            ReturnBox r = await Task.Run(() => MainService.Connect(SelectedConnection, status));
            if (r.ConnectStatus == ConnectStatus.OK)
                SelectedConnection = r.Connection;
            WorkDone(r);
        }
        private async void DisconnectAsync()
        {
            WorkStart("Disconnecting...");
            var status = new Progress<string>(ReportStatus);
            ReturnBox r = await Task.Run(() => MainService.Disconnect(SelectedConnection, status));
            if (r.ConnectStatus == ConnectStatus.OK)
            {
                SelectedConnection = r.Connection;
            }
            WorkDone(r);
        }
        

        private async void GetVersionsAsync()
        {
            Version = await Task.Run(() => MainService.GetVersions());
        }

        #endregion

        #region Command methods


        private void OnConnect(object obj)
        {
            if (IsWorking)
                return;

            Message = "";

            if (SelectedConnection == null 
                || SelectedConnection.ApCount() == 0
                || string.IsNullOrEmpty(SelectedConnection.Host))
            {
                //IsDriveNew = true;
                //SelectedAp = FreeDriveList.First();
                CurrentPage = Page.Host;
                return;
            }
            ConnectAsync();
            
        }
        private void OnConnectDisconnect(object obj)
        {
            if (SelectedConnection == null)
                return;
            if (SelectedConnection.IsConnected)
                DisconnectAsync();
            else
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
            if (r.ConnectStatus == ConnectStatus.OK)
                SelectedConnection = r.Connection;
            ConnectAsync();
        }
        private async void OnCloseAllApps(object obj)
        {
            WorkStart("Closing all...");
            var status = new Progress<string>(ReportStatus);
            await Task.Run(() =>
            {
                MainService.CloseAllApps(SelectedConnection, status);
            });
            WorkDone();
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
        
        private void OnSettingsDelete(object obj)
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

        private async void OnRunApp(Ap ap)
        {
            var r = new ReturnBox();
            var status = new Progress<string>(ReportStatus);

            if (!SelectedConnection.IsConnected)
                SelectedConnection.Connect();

            if (ap.Status != Status.STOPPED)
            {
                WorkStart($"Closing {ap.Name}...");
                await Task.Run(() => MainService.CloseAp(SelectedConnection, ap, status));
                r.Success = true;
            }
            else
            {
                ap.Status = Status.STARTING;
                // check display
                var disp = SelectedConnection.GetDisplay(ap.DisplayId);
                if (disp == null)
                {
                    r.Error = "Display not available";
                    r.Success = false;
                }
                else if (disp.Status == Status.STOPPED)
                {
                    WorkStart($"Starting DISPLAY :{disp.Id}...");                   
                    r = await Task.Run(() => MainService.XpraStart(SelectedConnection, disp, status));
                    if(r.Success)
                    {
                        WorkStart($"Attaching DISPLAY :{disp.Id}...");
                        r = await Task.Run(() => MainService.XpraAttach(SelectedConnection, disp, status));
                    }
                }
                else if (disp.Status == Status.DETACHED)
                {
                    WorkStart($"Attaching DISPLAY :{disp.Id}...");
                    r = await Task.Run(() => MainService.XpraAttach(SelectedConnection, disp, status));
                }
                else
                {
                    // display is active
                    r.Success = true;
                }

                if (r.Success)
                {
                    WorkStart($"Running {ap.Name}...");
                    //ap.Status = Status.STARTING;
                    r = await Task.Run(() => MainService.RunAp(SelectedConnection, ap, status));
                    if (r.Success)
                    {
                        if (int.TryParse(r.Output, out int pid))
                            ap.Pid = pid;
                        ap.Status = Status.ACTIVE;
                    }
                }
            }

            WorkDone(r);
        }
        private async void OnPlay(int display)
        {
            
            var r = new ReturnBox();
            var status = new Progress<string>(ReportStatus);
            var disp = SelectedConnection.GetDisplay(display);
            if (disp == null)
            {
                r.Error = "Display not available";
                r.Success = false;
            }
            else if (disp.Status == Status.STOPPED)
            {
                // start
                WorkStart($"Starting DISPLAY :{disp.Id}...");
                r = await Task.Run(() => MainService.XpraStart(SelectedConnection, disp, status));
                if (r.Success)
                {
                    // attach
                    WorkStart($"Attaching DISPLAY :{disp.Id}...");
                    r = await Task.Run(() => MainService.XpraAttach(SelectedConnection, disp, status));
                    if (r.Success)
                    {
                        disp.Status = Status.ACTIVE;
                    }
                }
            }
            else
            {
                // invalid status
            }

            WorkDone(r);
        }
        private async void OnPause(int display)
        {
            var r = new ReturnBox();
            var status = new Progress<string>(ReportStatus);
            var disp = SelectedConnection.GetDisplay(display);
            if (disp == null)
            {
                r.Error = "Display not available";
                r.Success = false;
            }
            else if (disp.Status == Status.ACTIVE)
            {
                WorkStart($"Detaching DISPLAY :{disp.Id}...");
                r = await Task.Run(() => MainService.Detach(disp, status));
                if (r.Success)
                {
                    disp.Status = Status.DETACHED;
                }
            }
            else 
            {
                // invalid status
            }

            WorkDone(r);
        }
        private async void OnResume(int display)
        {
            var r = new ReturnBox();
            var status = new Progress<string>(ReportStatus);
            var disp = SelectedConnection.GetDisplay(display);
            if (disp == null)
            {
                r.Error = "Display not available";
                r.Success = false;
            }
            else if (disp.Status == Status.DETACHED)
            {
                WorkStart($"Resuming DISPLAY :{disp.Id}...");
                r = await Task.Run(() => MainService.XpraAttach(SelectedConnection, disp, status));
                if (r.Success)
                {
                    disp.Status = Status.ACTIVE;
                }
            }
            else
            {
                // invalid status
            }

            WorkDone(r);
        }
        private async void OnStop(int display)
        {
            var r = new ReturnBox();
            var status = new Progress<string>(ReportStatus);
            var disp = SelectedConnection.GetDisplay(display);
            if (disp == null)
            {
                r.Error = "Display not available";
                r.Success = false;
            }
            else if (disp.Status == Status.ACTIVE || disp.Status == Status.DETACHED)
            {
                WorkStart($"Stopping DISPLAY :{disp.Id}...");
                r = await Task.Run(() => MainService.XpraStop(SelectedConnection, disp, status));
                if (r.Success)
                {
                   disp.Status = Status.STOPPED;
                }
            }
            else 
            {
                // invalid status
            }

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

        private ICommand _connectDisconnectCommand;
        public ICommand ConnectDisconnectCommand
        {
            get
            {
                return _connectDisconnectCommand ??
                    (_connectDisconnectCommand = new RelayCommand(OnConnectDisconnect));
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

        private ICommand _closeAllAppsCommand;
        public ICommand CloseAllAppsCommand
        {
            get
            {
                return _closeAllAppsCommand ??
                    (_closeAllAppsCommand = new RelayCommand(
                        x =>
                        {
                            OnCloseAllApps(x);
                        },
                        // can execute
                        x =>
                        {
                            //if(SelectedConnection != null)
                            //{
                            //    var ap = SelectedConnection.ApList.Where(y => y.Status != ApStatus.NOT_RUNNING).FirstOrDefault();
                            //    return ap != null;
                            //}
                            return true;
                        }));
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
                                //CheckApStatusAsync();
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
                        url => System.Diagnostics.Process.Start("explorer.exe", MainService.ExePath)));
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
                           OnRunApp((Ap)x);
                       },
                       // can execute
                       x =>
                       {
                           return true;
                       }));
            }
        }
        private ICommand _playCommand;
        public ICommand PlayCommand
        {
            get
            {
                return _playCommand ??
                   (_playCommand = new RelayCommand(
                       x =>
                       {
                           var display = int.Parse(x.ToString());
                           OnPlay(display);
                       },
                       // can execute
                       x =>
                       {
                           return true;
                       }));
            }
        }
        private ICommand _pauseCommand;
        public ICommand PauseCommand
        {
            get
            {
                return _pauseCommand ??
                   (_pauseCommand = new RelayCommand(
                       x =>
                       {
                           var display = int.Parse(x.ToString());
                           OnPause(display);
                       },
                       // can execute
                       x =>
                       {
                           return true;
                       }));
            }
        }
        private ICommand _resumeCommand;
        public ICommand ResumeCommand
        {
            get
            {
                return _resumeCommand ??
                   (_resumeCommand = new RelayCommand(
                       x =>
                       {
                           var display = int.Parse(x.ToString());
                           OnResume(display);
                       },
                       // can execute
                       x =>
                       {
                           return true;
                       }));
            }
        }
        private ICommand _stopCommand;
        public ICommand StopCommand
        {
            get
            {
                return _stopCommand ??
                   (_stopCommand = new RelayCommand(
                       x =>
                       {
                           var display = int.Parse(x.ToString());
                           OnStop(display);
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

