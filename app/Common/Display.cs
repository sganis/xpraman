using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace xpra
{
    public class Display : Observable
    {

        private DisplayStatus _status;

        public DisplayStatus Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;

                    foreach (var ap in ApList)
                        ap.UpdateStatus(Status);

                    NotifyPropertyChanged();
                    NotifyPropertyChanged("PlayButtonEnabled");
                    NotifyPropertyChanged("PauseButtonEnabled");
                    NotifyPropertyChanged("ResumeButtonEnabled");
                    NotifyPropertyChanged("StopButtonEnabled");
                    NotifyPropertyChanged("IsCheckingStatus");

                }
            }
        }
        private bool _isCheckingStatus;
        public bool IsCheckingStatus { 
            get { return _isCheckingStatus; }
            set {
                if (_isCheckingStatus != value)
                {
                    _isCheckingStatus = value;
                    NotifyPropertyChanged();
                }
            }
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

        public bool PlayButtonEnabled
        {
            get
            {
                return Status == DisplayStatus.NOT_USED;
            }
        }
        public bool PauseButtonEnabled
        {
            get
            {
                return Status == DisplayStatus.ACTIVE;
            }
        }
        public bool ResumeButtonEnabled
        {
            get
            {
                return Status == DisplayStatus.PAUSED;
            }
        }
        public bool StopButtonEnabled
        {
            get
            {
                return Status == DisplayStatus.PAUSED || Status == DisplayStatus.ACTIVE;
            }
        }
        public Display(int id)
        {
            ApList = new List<Ap>();
            Id = id;
            IsCheckingStatus = true;
        }
        public void AddApp(Ap a)
        {
            ApList.Add(a);
        }

    }
}
