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
        public ApStatus Status { get; set; }
        public int Display { get; set; }
        public Ap()
        {

        }
        public Ap(Ap d)
        {
            Clone(d);
        }
        public void Clone(Ap d)
        {
            if (d == null)
                return;

        }
        
       
        
    }

    public class DriveList : List<Ap>
    {
        public DriveList() { }
    }
}
