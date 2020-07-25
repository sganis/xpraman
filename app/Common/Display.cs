using System.Collections.Generic;

namespace xpra
{
    public class Display : TreeItem
    {

        private Status _status;
        public override Status Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;

                    foreach (var ap in ApList)
                        ap.UpdateStatus(_status);

                    NotifyPropertyChanged();
                    StatusChanged();
                }
            }
        }
        public override void StatusChanged()
        {
            NotifyPropertyChanged("IsEnabled");
            NotifyPropertyChanged("Opacity");
            NotifyPropertyChanged("PlayButtonEnabled");
            NotifyPropertyChanged("PauseButtonEnabled");
            NotifyPropertyChanged("ResumeButtonEnabled");
            NotifyPropertyChanged("StopButtonEnabled");
            NotifyPropertyChanged("PlayButtonVisible");
            NotifyPropertyChanged("PauseButtonVisible");
            NotifyPropertyChanged("ResumeButtonVisible");
            NotifyPropertyChanged("StopButtonVisible");
            NotifyPropertyChanged("IsWorking");
            NotifyPropertyChanged("IconColor");
            NotifyPropertyChanged("StatusText");
            foreach (var ap in ApList)
                ap.StatusChanged();


        }
        private int  _id;
        public int Id
        {
            get { return _id; }
            set
            {
                if (_id != value)
                {
                    _id = value;
                    NotifyPropertyChanged();                    
                }
            }
        }
        private List<Ap> _appList;
        public List<Ap> ApList
        {
            get { return _appList; }
            set
            {
                if (_appList != value)
                {
                    _appList = value;
                    NotifyPropertyChanged();
                }
            }
        }
        
        public bool PlayButtonVisible
        {
            get
            {
                return Status == Status.STOPPED 
                    || Status == Status.STARTING 
                    || Status == Status.STOPPING 
                    || Status == Status.CHECKING;
            }
        }
        public bool PauseButtonVisible
        {
            get
            {
                return Status == Status.ACTIVE || Status == Status.DETACHING;
            }
        }
        public bool ResumeButtonVisible
        {
            get
            {
                return Status == Status.DETACHED || Status == Status.ATTACHING;
            }
        }
        public bool StopButtonVisible
        {
            get
            {
                return true;
                //return Status == Status.DETACHED || Status == Status.RUNNING;
            }
        }
        public bool PlayButtonEnabled
        {
            get
            {
                return !IsWorking && Status != Status.CHECKING;
            }
        }
        public bool PauseButtonEnabled
        {
            get
            {
                return !IsWorking;
            }
        }
        public bool ResumeButtonEnabled
        {
            get
            {
                return !IsWorking;
            }
        }
        public bool StopButtonEnabled
        {
            get
            {
                return !IsWorking && (Status==Status.ACTIVE || Status == Status.DETACHED);
            }
        }
        public string IconColor
        {
            get
            {
                if (Status == Status.ACTIVE)
                    return "DarkGreen";
                if (Status == Status.STOPPED)
                    return "Black";
                if (Status == Status.DETACHED)
                    return "Purple"; // purple
                return "Black";
            }
        }
       

        public string StatusText
        {
            get
            {
                return
                    _status == Status.ACTIVE ? "ACTIVE" :
                    _status == Status.DETACHED ? "PAUSED" :
                    _status == Status.STARTING ? "Starting..." :
                    _status == Status.ATTACHING ? "Resuming..." :
                    _status == Status.DETACHING ? "Pausing..." :
                    _status == Status.STOPPING ? "Stopping..." :
                    "";
            }
        }
        public Display(int id)
        {
            ApList = new List<Ap>();
            Id = id;
            //IsEnabled = true;
            Status = Status.CHECKING;
        }
        public void AddApp(Ap a)
        {
            ApList.Add(a);

        }

    }
}
