using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace xpra
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Ap : Observable
    {
        public int Pid { get; set; }
        public int Pgid { get; set; }
        public string Path { get; set; }
        public string Process { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }
        private DisplayStatus _displayStatus;
        public DisplayStatus DisplayStatus { 
            get { return _displayStatus;  }
            set
            {
                if (_displayStatus != value)
                {
                    _displayStatus = value;
                    NotifyPropertyChanged();
                    //NotifyPropertyChanged("AttachButtonText");
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
                    NotifyPropertyChanged("StatusText");
                    NotifyPropertyChanged("RunButtonText");
                    NotifyPropertyChanged("RunButtonColor");
                }
            }
        }
        public void UpdateStatus(DisplayStatus dispStatus)
        {
            switch (Status)
            {
                case ApStatus.RUNNING:
                    if (dispStatus == DisplayStatus.NOT_USED)
                        Status = ApStatus.NOT_RUNNING;
                    else if (dispStatus == DisplayStatus.PAUSED)
                        Status = ApStatus.BACKGROUND;
                    break;
                case ApStatus.BACKGROUND:
                    if (dispStatus == DisplayStatus.NOT_USED)
                        Status = ApStatus.NOT_RUNNING;
                    else if (dispStatus == DisplayStatus.ACTIVE)
                        Status = ApStatus.RUNNING;
                    break;
            }

        }

        public string StatusText
        {
            get
            {
                return _status == ApStatus.RUNNING ? "Running" :
                    _status == ApStatus.BACKGROUND ? "Background" :
                    "";
            }
        }
        public int DisplayId { get; set; }
        
        public string RunButtonText { 
            get 
            {
                if (Status == ApStatus.NOT_RUNNING)
                    return "RUN";
                return "CLOSE";
            }
        }
        public string RunButtonColor
        {
            get
            {
                if (Status == ApStatus.NOT_RUNNING)
                    return "SlateGray"; 
                if (Status == ApStatus.BACKGROUND)
                    return "DarkMagenta"; // purple
                if (Status == ApStatus.RUNNING)
                    return "ForestGreen";
                // unknown 
                return "DimGray";
               
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
