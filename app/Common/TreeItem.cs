
using System.Diagnostics.Eventing.Reader;

namespace xpra
{
    public abstract class TreeItem : Observable
    {
        public Connection Connection { get; set; }

        public bool IsEnabled
        {
            get { return Connection != null && Connection.IsConnected; }
        }
        private bool _isExpanded;
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public string Opacity
        {
            get { return IsEnabled ? "1.0" : "0.5"; }
        }

        private Status _status;
        public virtual Status Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    NotifyPropertyChanged();
                    StatusChanged();
                }
            }
        }
        
        public bool IsWorking
        {
            get {
                return Status == Status.STARTING
                  || Status == Status.STOPPING
                  || Status == Status.ATTACHING
                  || Status == Status.DETACHING;}
        }

        public virtual void StatusChanged()
        {

        }
        public virtual string ItemId()
        {
            return "n/a";
        }
    }
}
