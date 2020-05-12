#pragma warning disable CS0168
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace xpra
{
    [JsonObject(MemberSerialization.OptOut)]
    public class Settings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public Dictionary<string, Ap> Aps { get; set; }

        public void AddAps(IEnumerable<Ap> aps)
        {
            Aps.Clear();
            foreach (var d in aps)
                Aps[d.Name] = d;
        }
        public void AddAp(Ap a)
        {
            //if (Aps.ContainsKey(drive.Name))
            //{
            //    var d = Aps[drive.Name];
            //    d.MountPoint = drive.MountPoint;
            //    d.Label = drive.Label;
            //    d.Args = drive.Args;
            //    d.Status = drive.Status;
            //}
            //else
            //{
            //    Aps[drive.Name] = drive;
            //}
        }

        [JsonIgnore]
        public string Filename { get; set; }

        public Settings()
        {
            Aps = new Dictionary<string, Ap>();
        }

        internal void Load()
        {
            if (!File.Exists(Filename))
                return;

            try
            {
                var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(Filename));
                if (json.ContainsKey("host") && json["host"] != null)
                    Host = json["host"].ToString();
                if (json.ContainsKey("port") && json["port"] != null)
                    Port = int.Parse(json["port"].ToString());
                if (json.ContainsKey("user") && json["user"] != null)
                    User = json["user"].ToString();

                if (!json.ContainsKey("aps"))
                    return;
                var aps = JsonConvert.DeserializeObject<List<object>>(json["aps"].ToString());
                int i=0;
                foreach (var a in aps)
                {
                    var ap = JsonConvert.DeserializeObject<Dictionary<string, string>>(a.ToString());
                    Ap d = new Ap
                    {
                        Name = ap["name"],
                        Path = ap["path"],
                        Display = 100 + (++i)
                    };

                    //var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(_drives[_d].ToString());
                    //if (data.ContainsKey("Args") && data["Args"] != null)
                    //    d.Args = data["Args"].ToString();
                    //if (data.ContainsKey("Label") && data["Label"] != null)
                    //    d.Label = data["Label"].ToString();
                    //if (data.ContainsKey("MountPoint") && data["MountPoint"] != null)
                    //    d.MountPoint = data["MountPoint"].ToString();
                    ////if (data.ContainsKey("Hosts") && data["Hosts"] != null)
                    ////    d.Hosts = JsonConvert.DeserializeObject<List<string>>(data["Hosts"].ToString());
                    Aps[d.Name] = d;
                }
                
            }
            catch (Exception ex)
            {
            }
        }
    }
}
