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
                var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(Filename));
                
                // todo: escape and remove semicolon to avoid command injection
                if (json.ContainsKey("xpra_server_args"))
                XpraServerArgs = json["xpra_server_args"].ToString();
                if (json.ContainsKey("xpra_client_args"))
                    XpraClientArgs = json["xpra_client_args"].ToString();

                

                if (!json.ContainsKey("apps"))
                    return;
                var apps = JsonConvert.DeserializeObject<List<object>>(json["apps"].ToString());

                foreach (var a in apps)
                {
                    var ap = JsonConvert.DeserializeObject<Dictionary<string, string>>(a.ToString());
                    Ap appobj = new Ap
                    {
                        Name = ap["name"],
                        Path = ap["path"],
                        Host = ap["host"],
                        DisplayId = int.Parse("0" + ap["display"]),

                    };
                    if (ap.ContainsKey("process"))
                        appobj.Process = ap["process"];
                    else
                        appobj.Process = ap["path"];
                    Connection conn = ConnectionList.Where(x => x.Host == appobj.Host).FirstOrDefault();
                    if (conn == null)
                    {
                        conn = new Connection();
                        conn.Url = $"ssh://{conn.CurrentUser}@{appobj.Host}:{conn.CurrentPort}";
                        ConnectionList.Add(conn);
                    }
                    conn.AddApp(appobj);

                    //Connection connobj = new Connection();
                    //var conn = JsonConvert.DeserializeObject<Dictionary<string, object>>(c.ToString());
                    //if (conn.ContainsKey("url"))
                    //    connobj.Url = conn["url"].ToString();
                    //if (conn.ContainsKey("nickname"))
                    //    connobj.Nickname = conn["nickname"].ToString();
                    //if (!conn.ContainsKey("apps"))
                    //    return;
                    //var aps = JsonConvert.DeserializeObject<List<object>>(conn["apps"].ToString());
                    //foreach (var a in aps)
                    //{
                    //    var ap = JsonConvert.DeserializeObject<Dictionary<string, string>>(a.ToString());
                    //    Ap appobj = new Ap
                    //    {
                    //        Name = ap["name"],
                    //        Path = ap["path"],
                    //        DisplayId = int.Parse("0"+ ap["display"]),
                    //    };
                    //    connobj.AddApp(appobj);
                        
                    //}
                    //ConnectionList.Add(connobj);
                }                
            }
            catch (Exception ex)
            {
            }
        }
    }
}
