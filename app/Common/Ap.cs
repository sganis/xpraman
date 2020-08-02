using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Permissions;

namespace xpra
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Ap : TreeItem
    {
        public string Path { get; set; }
        public string Process { get; set; }
        public string Name { get; set; }
        public string Host { get; set; }
        
        public Display Display { get; set; }

        public string InstanceCountText
        {
            get
            {
                return InstanceList.Count > 1 ? $"({ InstanceList.Count })" : "";
            }
        }
        public ObservableCollection<Instance> _instances;
        public ObservableCollection<Instance> InstanceList
        {
            get { return _instances; }
            set
            {
                if (_instances != value)
                {
                    _instances = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged("InstanceCountText");
                }
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
            NotifyPropertyChanged("InstanceList");
            NotifyPropertyChanged("InstanceCountText");
        }
        public override string ItemId()
        {
            return $"{Display.ItemId()}-{Name}";
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
                return !IsWorking && Status != Status.UNKNOWN;
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
            Status = Status.UNKNOWN;
            _instances = new ObservableCollection<Instance>();
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
        public void AddInstance(string pgid, string pid, string process=null)
        {
            Instance i = InstanceList.Where(x => x.Pgid == pgid).FirstOrDefault();
            if (i == null)
            {
                i = InstanceList.Where(x => x.Pid == pid).FirstOrDefault();
                if (i == null)
                {
                    i = new Instance(pgid);
                    if (pid != null)
                        i.Pid = pid;
                    if (process != null)
                        i.Process = process;

                    InstanceList.Add(i);
                    NotifyPropertyChanged("InstanceList");
                    NotifyPropertyChanged("InstanceCountText");
                }
                else
                {
                    i.Pgid = pgid;
                    i.Process = process;                    
                }
            }
            i.IsUpdated = true;
        }
        public void RemoveInstance(Instance i)
        {
            _instances.Remove(i);
            NotifyPropertyChanged("InstanceList");
            NotifyPropertyChanged("InstanceCountText");
        }

        public void ClearInstances()
        {
            _instances.Clear();
            NotifyPropertyChanged("InstanceList");
            NotifyPropertyChanged("InstanceCountText");
        }

    }

}
