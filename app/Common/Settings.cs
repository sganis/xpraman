#pragma warning disable CS0168
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace xpra
{
    [JsonObject(MemberSerialization.OptOut)]
    public class Settings
    {
        public List<Connection> ConnectionList { get; set; }
        public string XpraServerArgs { get; set; }
        public string XpraClientArgs { get; set; }
        
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
                
                // todo: escape and remove semicolon to avoid command injection
                if (json.ContainsKey("xpra_server_args"))
                XpraServerArgs = json["xpra_server_args"].ToString();
                if (json.ContainsKey("xpra_client_args"))
                    XpraClientArgs = json["xpra_client_args"].ToString();

                

                if (!json.ContainsKey("apps"))
                    return;
                var apps = JsonConvert.DeserializeObject<List<object>>(json["apps"].ToString());

                Dictionary<int, Display> displays = new Dictionary<int, Display>();

                foreach (var a in apps)
                {
                    var ap = JsonConvert.DeserializeObject<Dictionary<string, string>>(a.ToString());
                    
                    Connection conn = ConnectionList.Where(x => x.Host == ap["host"]).FirstOrDefault();
                    if (conn == null)
                    {
                        conn = new Connection();
                        conn.Url = $"ssh://{conn.CurrentUser}@{ap["host"]}:{conn.CurrentPort}";
                        ConnectionList.Add(conn);
                    }
                    
                    var displayId = int.Parse("0" + ap["display"]);
                    Display disp = null;
                    if (!displays.ContainsKey(displayId))
                    {
                        disp = new Display(displayId);
                        disp.Connection = conn; 
                        displays[displayId] = disp;
                    }
                    disp = displays[displayId];
                    
                    Ap appobj = new Ap(disp)
                    {
                        Name = ap["name"],
                        Path = ap["path"],
                        Host = ap["host"],                    
                    };
                    if (ap.ContainsKey("process"))
                        appobj.Process = ap["process"];
                    else
                        appobj.Process = ap["path"];
                   
                    appobj.Connection = conn;
                    conn.AddApp(appobj);

                }                
            }
            catch (Exception ex)
            {
            }
        }
    }
}
