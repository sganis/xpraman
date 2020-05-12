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

        private ApService _apService;

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
        
        public ObservableCollection<Ap> Aps { get; set; } = new ObservableCollection<Ap>();
        private Ap _selectedAp;
        public Ap SelectedAp
        {
            get { return _selectedAp; }
            set
            {                  
                if (_selectedAp != value)
                {
                    _selectedAp = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged("HasApps");                    
                }
            }            
        }

        public bool HasApps
        { 
            get 
            { 
                return _apService.Aps != null && _apService.Aps.Count > 0; 
            }
        }

        private DriveStatus driveStatus;
        public DriveStatus DriveStatus
        {
            get { return driveStatus; }
            set
            {
                driveStatus = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("ConnectButtonText");
                NotifyPropertyChanged("ConnectButtonColor");
            }
        }

        private MountStatus _mountStatus;
        public MountStatus MountStatus
        {
            get { return _mountStatus; }
            set
            {
                _mountStatus = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("MessageColor");
                NotifyPropertyChanged("ConnectButtonIsEnabled");

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
        
        public string ConnectButtonText => 
            (DriveStatus == DriveStatus.CONNECTED 
            || DriveStatus == DriveStatus.BROKEN ) ? "Disconnect" : "Connect";
        public string ConnectButtonColor => DriveStatus == DriveStatus.CONNECTED ? "#689F38" : "#607d8b";
        public bool ConnectButtonIsEnabled => true;
        public bool IsSettingsChanged { get; set; }

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
            _apService = new ApService();
            //Messenger.Default.Register<string>(this, OnShowView);
            
            CurrentPage = Page.Main;
            LoadApsAsync();
            GetVersionsAsync();
            if (rb != null)
                Message = rb.Error;           
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
            DriveStatus = r.DriveStatus;
            MountStatus = r.MountStatus;
            Message = r.Error;

            switch (r.MountStatus)
            {
                case MountStatus.BAD_CLI:
                case MountStatus.BAD_WINFSP:
                case MountStatus.BAD_DRIVE:
                    CurrentPage = Page.Main;
                    break;
                case MountStatus.BAD_HOST:
                    CurrentPage = Page.Host;
                    OnFocusRequested(nameof(SelectedAp.Host));
                    break;
                case MountStatus.BAD_PASSWORD:
                case MountStatus.BAD_KEY:
                    CurrentPage = Page.Password;
                    OnFocusRequested(nameof(Password));
                    break;
                case MountStatus.OK:
                    CurrentPage = Page.Main;
                    Message = r.DriveStatus.ToString();
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
            await Task.Run(() =>
            {
                Settings settings = _apService.LoadSettings();
                if (_selectedAp == null && settings.SelectedAp != null)
                    SelectedAp = settings.SelectedAp;
                //_apService.UpdateDrives(settings);
            });
            
            //UpdateObservableDrives();

            if (_apService.Aps.Count == 0)
            {
                CurrentPage = Page.Host;
                OnFocusRequested(nameof(SelectedAp.Host));
                //SelectedAp = FreeDriveList.First();
                WorkDone();
            }
            else
            {
                CheckDriveStatusAsync();
            }
            Loaded = true;

        }

        private void UpdateObservableDrives()
        {
            //Ap old = null;
            //if (SelectedAp != null)
            //    old = SelectedAp;
            //GoldDriveList.Clear();
            //FreeDriveList.Clear();
            //_apService.GoldDrives.ForEach(GoldDriveList.Add);
            //_apService.FreeDrives.ForEach(FreeDriveList.Add);
            //if (old != null && SelectedAp == null)
            //    SelectedAp = old;

            //if (SelectedAp != null)
            //{
            //    var d1 = _apService.GoldDrives.ToList().Find(x => x.Name == SelectedAp.Name);
            //    if (d1 != null)
            //    {
            //        d1.Clone(SelectedAp);
            //        SelectedAp = d1;
            //    }
            //    else
            //    {
            //        var d2 = _apService.FreeDrives.ToList().Find(x => x.Name == SelectedAp.Name);
            //        if (d2 != null)
            //        {
            //            d2.Clone(SelectedAp);
            //            SelectedAp = d2;
            //        }
            //    }
            //}
            //else
            //{
            //    if (_apService.GoldDrives.Count > 0)
            //    {
            //        SelectedAp = _apService.GoldDrives.First();
            //    }
            //    else if (_apService.FreeDrives.Count > 0)
            //    {
            //        SelectedAp = _apService.FreeDrives.First();
            //    }
            //}

            //NotifyPropertyChanged("FreeDriveList");
            //NotifyPropertyChanged("GoldDriveList");
        }

        void ReportStatus(string message)
        {
            Message = message;
        }

        private async void ConnectAsync(Ap ap)
        {
            WorkStart("Connecting...");
            var status = new Progress<string>(ReportStatus);
            ReturnBox r = await Task.Run(() => _apService.Connect(ap, status));
            SkipComboChanged = true;
            UpdateObservableDrives();
            SkipComboChanged = false;
            WorkDone(r);
        }

        private async void CheckDriveStatusAsync()
        {
            if (SelectedAp != null)
            {
                //WorkStart("Checking status...");
                //ReturnBox r = await Task.Run(() => _apService.CheckDriveStatus(SelectedAp));
                //WorkDone(r);
            }
        }

        private async void GetVersionsAsync()
        {
            Version = await Task.Run(() => _apService.GetVersions());
        }

        #endregion

        #region Command methods

        private async void OnConnect(object obj)
        {
            if (IsWorking)
                return;

            Message = "";

            if (Aps.Count == 0 || string.IsNullOrEmpty(SelectedAp.Host))
            {
                //IsDriveNew = true;
                //SelectedAp = FreeDriveList.First();
                CurrentPage = Page.Host;
                return;
            }
            if (ConnectButtonText == "Connect")
            {
                ConnectAsync(SelectedAp);
            }
            else
            {
                WorkStart("Disconnecting...");
               // ReturnBox r = await Task.Run(() => _apService.Unmount(SelectedAp));
                //WorkDone(r);
            }

        }

        private void OnConnectHost(object obj)
        {
            if (string.IsNullOrEmpty(SelectedAp.Host))
            {
                Message = "Server is required";
                OnFocusRequested(nameof(SelectedAp.Host));
                return;
            }
            ConnectAsync(SelectedAp);
        }

        private async void OnConnectPassword(object obj)
        {
            if (SelectedAp == null)
            {
                Message = "Invalid drive";
                return;
            }

            WorkStart("Connecting...");
            var status = new Progress<string>(ReportStatus);
            ReturnBox r = await Task.Run(() => _apService.ConnectPassword(SelectedAp, password, status));
            SkipComboChanged = true;
            UpdateObservableDrives();
            SkipComboChanged = false;
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
            if (SelectedAp == null)
            {
                Message = "Invalid drive";
                return;
            }

            SelectedAp.Trim();
            if (string.IsNullOrEmpty(SelectedAp.Host))
            {
                Message = "Server is required";
                OnFocusRequested("SelectedAp.Host");
                return;
            }

            Regex hostRegex = new Regex(HostRegex);
            if (!hostRegex.Match(SelectedAp.Host).Success 
                && !hostRegex.Match(SelectedAp.Host).Success)
            {
                Message = "Invalid server name";
                OnFocusRequested("SelectedAp.Host");
                return;
            }
            await Task.Run(() =>
            {
                Settings settings = _apService.LoadSettings();
                settings.AddDrive(SelectedAp);
                _apService.SaveSettings(settings);
                //_apService.UpdateDrives(settings);
            });

            UpdateObservableDrives();
            Message = "";
            //IsDriveNew = false;
            //IsDriveEdit = false;

        }
        private void OnSettingsCancel(object obj)
        {
            if (SelectedAp == null)
            {
                Message = "Invalid drive";
                return;
            }

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
            if (SelectedAp == null)
            {
                Message = "Invalid drive";
                return;
            }

            //Ap d = SelectedAp;
            //if (GoldDriveList.Contains(d))
            //    GoldDriveList.Remove(d);
            //await Task.Run(() =>
            //{
            //    if (d.Status == DriveStatus.CONNECTED)
            //        _apService.Unmount(d);
            //    Settings settings = _apService.LoadSettings();
            //    settings.AddDrives(GoldDriveList);
            //    _apService.SaveSettings(settings);
            //    _apService.UpdateDrives(settings);
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
            Settings settings = _apService.LoadSettings();
            if (Aps != null)
            {
                //settings.Selected = SelectedAp != null ? SelectedAp.Name : "";
                //settings.AddApps(Aps.ToList());
                //_apService.SaveSettings(settings);
            }
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
                                CheckDriveStatusAsync();
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
                        url => System.Diagnostics.Process.Start("explorer.exe", _apService.LocalAppData)));
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
            
            CheckDriveStatusAsync();
        }

        #endregion

    }
}

