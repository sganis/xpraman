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
        private ApStatus _status;
        public ApStatus Status { get { return _status; }
            set 
            {
                if (_status != value)
                {
                    _status = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged("RunButtonText");
                    NotifyPropertyChanged("RunButtonColor");
                }
            }
        }
        public int Display { get; set; }
        public string RunButtonText { 
            get 
            {
                if (Status == ApStatus.NOT_RUNNING)
                    return "RUN";
                if (Status == ApStatus.IDLE)
                    return "RESUME";
                if (Status == ApStatus.RUNNING)
                    return "PAUSE";
                return "ERROR";
            }
        }
        public string RunButtonColor
        {
            get
            {
                if (Status == ApStatus.NOT_RUNNING)
                    return "Blue";
                if (Status == ApStatus.IDLE)
                    return "Red";
                if (Status == ApStatus.RUNNING)
                    return "Green";
                return "ERROR";
            }
        }
        public Ap()
        {

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
