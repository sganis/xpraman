﻿#pragma warning disable CS0168
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
                // parse config.json
                var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(Filename));
                
                // todo: escape and remove semicolon to avoid command injection
                if (json.ContainsKey("xpra_server_args"))
                XpraServerArgs = string.Join(" ", JsonConvert.DeserializeObject<List<string>>(json["xpra_server_args"].ToString()));
                if (json.ContainsKey("xpra_client_args"))
                    XpraClientArgs = string.Join(" ", JsonConvert.DeserializeObject<List<string>>(json["xpra_client_args"].ToString()));



                if (!json.ContainsKey("apps"))
                    return;
                var apps = JsonConvert.DeserializeObject<List<object>>(json["apps"].ToString());

                var displays = new Dictionary<string, Display>();

                foreach (var a in apps)
                {
                    var ap = JsonConvert.DeserializeObject<Dictionary<string, string>>(a.ToString());
                    
                    Connection conn = ConnectionList.Where(x => x.Url == ap["url"]).FirstOrDefault();
                    if (conn == null)
                    {
                        conn = new Connection();
                        conn.Url = ap["url"];
                        ConnectionList.Add(conn);
                    }
                    
                    var display = ap["display"];
                    var displayId = $"{conn.Uid}{display}";
                    Display disp = null;
                    var dispkey = $"{conn.Url}/{display}";
                    if (!displays.ContainsKey(dispkey))
                    {
                        disp = new Display(displayId);
                        disp.Connection = conn; 
                        displays[dispkey] = disp;
                        conn.AddDisplay(disp);
                    }
                    disp = displays[dispkey];
                    
                    Ap appobj = new Ap(disp)
                    {
                        Name = ap["name"],
                        Path = ap["path"],                   
                    };
                    if (ap.ContainsKey("process"))
                        appobj.Process = ap["process"];
                    else
                        appobj.Process = ap["path"];
                   
                    appobj.Connection = conn;
                    disp.AddApp(appobj);

                }

                // get expanded property form settings
                if (!String.IsNullOrEmpty(Properties.Settings.Default.Expanded))
                {
                    var expanded = JsonConvert.DeserializeObject<Dictionary<string, bool>>(Properties.Settings.Default.Expanded);
                    foreach (var conn in this.ConnectionList)
                    {
                        foreach (var display in conn.DisplayList)
                        {
                            if (expanded.ContainsKey(display.ItemId()))
                                display.IsExpanded = expanded[display.ItemId()];
                            foreach (var ap in display.ApList)
                            {
                                if (expanded.ContainsKey(ap.ItemId()))
                                    ap.IsExpanded = expanded[ap.ItemId()];

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
    }
}
