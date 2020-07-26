using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xpra
{
    public class Instance : TreeItem
    {
        public string Pid { get; set; }
        public string Process { get; set; }
        public bool IsUpdated { get; set; }
        string _pgid;
        public string Pgid {
            get { return _pgid; }
            set
            {
                if (_pgid != value)
                {
                    _pgid = value;
                    NotifyPropertyChanged();
                }
            }
        }

        
        public Instance(string pgid)
        {
            Pgid = pgid;
        }
    }
}
