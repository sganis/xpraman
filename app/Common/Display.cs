using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                    NotifyPropertyChanged();
                    NotifyPropertyChanged("AttachButtonText");

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
        public string AttachButtonText
        {
            get
            {
                if (Status == DisplayStatus.NOT_USED)
                    return "START";
                if (Status == DisplayStatus.IDLE)
                    return "ATTACH";
                if (Status == DisplayStatus.ACTIVE)
                    return "DETACH";
                return "N/A";
            }
        }
        public Display(int id)
        {
            ApList = new List<Ap>();
            Id = id;
        }
        public void AddApp(Ap a)
        {
            ApList.Add(a);
        }

    }
}
