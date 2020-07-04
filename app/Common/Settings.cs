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
        public List<Connection> ConnectionList { get; set; }

        [JsonIgnore]
        public string Filename { get; set; }

        public Settings()
        {
            ConnectionList = new List<Connection>();
        }

        internal void Load()
        {
            if (!File.Exists(Filename))
                return;

            try
            {
                var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(Filename));
                if (!json.ContainsKey("connections"))
                    return;
                var conns = JsonConvert.DeserializeObject<List<object>>(json["connections"].ToString());
                foreach (var c in conns)
                {
                    Connection connobj = new Connection();
                    var conn = JsonConvert.DeserializeObject<Dictionary<string, object>>(c.ToString());
                    if (conn.ContainsKey("url"))
                        connobj.Url = conn["url"].ToString();
                    if (conn.ContainsKey("nickname"))
                        connobj.Nickname = conn["nickname"].ToString();
                    if (!conn.ContainsKey("apps"))
                        return;
                    var aps = JsonConvert.DeserializeObject<List<object>>(conn["apps"].ToString());
                    foreach (var a in aps)
                    {
                        var ap = JsonConvert.DeserializeObject<Dictionary<string, string>>(a.ToString());
                        Ap appobj = new Ap
                        {
                            Name = ap["name"],
                            Path = ap["path"],
                            Display = int.Parse("0"+ ap["display"]),
                        };
                        connobj.AddApp(appobj);
                        
                    }
                    ConnectionList.Add(connobj);
                }                
            }
            catch (Exception ex)
            {
            }
        }
    }
}
