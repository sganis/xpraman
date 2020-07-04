using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace xpra
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Ap : Observable
    {
        public string Path { get; set; }
        public string Name { get; set; }
        private DisplayStatus _displayStatus;
        public DisplayStatus DisplayStatus { 
            get { return _displayStatus;  }
            set
            {
                if (_displayStatus != value)
                {
                    _displayStatus = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged("AttachButtonText");
                    NotifyPropertyChanged("RunButtonText");
                    NotifyPropertyChanged("RunButtonColor");
                    NotifyPropertyChanged("ApList");

                }
            }
        }

        private ApStatus _status;
        public ApStatus Status { get { return _status; }
            set 
            {
                if (_status != value)
                {
                    _status = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged("AttachButtonText");
                    NotifyPropertyChanged("RunButtonText");
                    NotifyPropertyChanged("RunButtonColor");
                }
            }
        }
        public int Display { get; set; }
        public string AttachButtonText
        {
            get
            {
                if (Status == ApStatus.NOT_RUNNING)
                    return "RUN";
                if (Status == ApStatus.BACKGROUND)
                    return "RESUME";
                if (Status == ApStatus.ACTIVE)
                    return "PAUSE";
                return "N/A";
            }
        }
        public string RunButtonText { 
            get 
            {
                if (Status == ApStatus.NOT_RUNNING)
                    return "RUN";
                if (Status == ApStatus.BACKGROUND)
                    return "RESUME";
                if (Status == ApStatus.ACTIVE)
                    return "PAUSE";
                return "N/A";
            }
        }
        public string RunButtonColor
        {
            get
            {
                //if (Status == ApStatus.NOT_RUNNING)
                //    return "Purple";
                if (Status == ApStatus.BACKGROUND)
                    return "Purple";
                if (Status == ApStatus.ACTIVE)
                    return "ForestGreen";
                if (Status == ApStatus.UNKNOWN)
                    return "DimGray";
                return "CadetBlue";
            }
        }
        public Ap()
        {
            DisplayStatus = DisplayStatus.NOT_USED;
        }
        public Ap(Ap d)
        {
            Clone(d);
        }
        public void Clone(Ap d)
        {
            // todo
            if (d == null)
                return;

        }
        
       
        
    }

}
