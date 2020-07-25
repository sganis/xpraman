using Newtonsoft.Json;
using System.Collections.Generic;
using System.Security.Permissions;

namespace xpra
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Ap : TreeItem
    {
        //public int Pid { get; set; }
        //public int Pgid { get; set; }
        public string Path { get; set; }
        public string Process { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }
        public Display Display { get; set; }
        private List<int> _pgids;
        public List<int> ProcessGroupIds
        {
            get { return _pgids; }
            set
            {
                if (_pgids != value)
                {
                    _pgids = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged("InstanceCount");
                }
            }
        }
        public string InstanceCount
        {
            get
            {
                return ProcessGroupIds.Count > 1 ? $"({ ProcessGroupIds.Count })" : "";
            }
        }

        public override void StatusChanged()
        {
            NotifyPropertyChanged("IsWorking");
            NotifyPropertyChanged("IsEnabled");
            NotifyPropertyChanged("IconColor");
            NotifyPropertyChanged("Opacity");
            NotifyPropertyChanged("StatusText");
            NotifyPropertyChanged("PlayButtonEnabled");
            NotifyPropertyChanged("StopButtonEnabled");
            NotifyPropertyChanged("InstanceCount");
        }
        public void UpdateStatus(Status dispStatus)
        {
            if (Status == Status.ACTIVE || Status == Status.DETACHED)
                Status = dispStatus;
        }
        public string IconColor
        {
            get
            {
                if (Status == Status.STOPPED)
                    return "SlateGray";
                if (Status == Status.DETACHED)
                    return "DarkMagenta"; // purple
                if (Status == Status.ACTIVE)
                    return "ForestGreen";
                // unknown 
                return "DimGray";

            }
        }
        public string StatusText
        {
            get
            {
                return
                    Status == Status.STARTING ? "Starting..." :
                    Status == Status.ACTIVE ? "Running" :
                    Status == Status.DETACHED ? "Background" :
                    Status == Status.STARTING ? "Starting..." :
                    Status == Status.ATTACHING ? "Attaching..." :
                    Status == Status.DETACHING ? "Detaching..." :
                    Status == Status.STOPPING ? "Closing..." :
                    "";
            }
        }
        
        public int DisplayId { get { return Display.Id;  } }
        
        public bool PlayButtonEnabled
        {
            get
            {
                return !IsWorking && Status != Status.CHECKING;
            }
        }
        
        public bool StopButtonEnabled
        {
            get
            {
                return !IsWorking && (Status == Status.ACTIVE || Status == Status.DETACHED);
            }
        }
        public Ap(Display d)
        {
            Display = d;
            //IsEnabled = true;
            _pgids = new List<int>();
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
        public void AddPgid(int p)
        {
            ProcessGroupIds.Add(p);
            NotifyPropertyChanged("InstanceCount");
        }
        public void ClearPgids()
        {
            _pgids.Clear();
            NotifyPropertyChanged("InstanceCount");
        }

    }

}
