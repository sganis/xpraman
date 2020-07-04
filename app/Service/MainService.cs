﻿#pragma warning disable CS0168
using Microsoft.Win32;
using Newtonsoft.Json;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.ServiceProcess;
using System.Text.RegularExpressions;

namespace xpra
{

    public class MainService : Observable
    {
        #region Properties

        string m_xpra_local;


        public ObservableCollection<Connection> ConnectionList { get; set; } = new ObservableCollection<Connection>();
        public Connection SelectedConnection { get; set; } = new Connection();
        
        public string ExePath
        {
            get
            {
                return System.AppDomain.CurrentDomain.BaseDirectory;
            }
        }
        
        private string localAppData;
        public string LocalAppData
        {
            get
            {
                if (localAppData == null)
                    localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\XpraAppManager";
                return localAppData;
            }
        }

        #endregion

        public MainService( string xpra_local)
        {
            m_xpra_local = xpra_local;
        }

        #region Serialization

        public void UpdateFromSettings(Settings settings)
        {
            if (settings.ConnectionList.Count > 0) {
                foreach (var c in settings.ConnectionList)
                    ConnectionList.Add(c);

                SelectedConnection = ConnectionList.Where(x => x.Default).FirstOrDefault();
                if (SelectedConnection == null)
                    SelectedConnection = ConnectionList.First();            
            }

        }

        public Settings LoadSettings()
        {
            Settings settings = new Settings() {
                Filename = ExePath + "config.json" 
            };
            settings.Load();
            return settings;
        }

        public void SaveSettings(Settings settings)
        {
            try
            {
                settings.Filename = ExePath + "config.json";
                
                using (var file = File.CreateText(settings.Filename))
                {
                    var json = JsonConvert.SerializeObject(
                        settings,
                        Newtonsoft.Json.Formatting.Indented,
                        new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        });
                    file.Write(json);
                }
            }
            catch (Exception ex)
            {
            }
        }


        #endregion

        

        #region Ap Management


        public ReturnBox RunAp(Connection conn, Ap ap, IProgress<string> status)
        {
            ReturnBox r = new ReturnBox();
            
            // run in server
            if(!conn.Connected)
            {
                var msg = "Not connected";
                r.Error = msg;
                return r;
            }
            status.Report("Starting xpra server...");
            r = XpraStart(conn, ap);
            if (!r.Success)
                return r;

            status.Report($"Starting {ap.Name}...");
            var cmd = $"export DISPLAY=:{ap.Display}; \"{ap.Path}\"";
            r = conn.RunRemote(cmd);
            if (!r.Success)
                return r;

            // attach
            status.Report("Attaching display...");
            return XpraAttach(conn, ap);
        }
        public ReturnBox XpraStart(Connection conn, Ap ap)
        {
            ReturnBox r = new ReturnBox();
            // check if xpra is running in display


            // todo: get extra args from config
            var extra_server_args = "";
            var cmd = $"xpra start :{ap.Display} --start-child=\"{ap.Path}\" --exit-with-children {extra_server_args} ";
            r = conn.RunRemote(cmd);

            return r;
        }
        public ReturnBox XpraAttach(Connection conn, Ap ap)
        {
            var cmd = m_xpra_local;
            var extra_local_args = "--microphone=off --speaker=off --tray=no --dpi=100 --webcam=no --idle-timeout=0 --cursors=yes --opengl=yes --compress=0";
            var args = $"attach ssh://{conn.CurrentUser}@{conn.Host}/{ap.Display} --exit-with-children {extra_local_args}";
            return conn.RunLocal(cmd, args, false);
        }
        public void Detach(int display)
        {
            var r = new ReturnBox();
            string cmd = "";
            foreach (var p in Process.GetProcesses())
            {
                if (p.ProcessName.Contains("Xpra"))
                {
                    cmd = ProcCmdLine.GetCommandLineOfProcess(p);
                    if (Regex.IsMatch(cmd, $@"attach ssh://.+/{display} --exit-with-children"))
                    {
                        p.Kill();
                        break;
                    }
                }
            }
        }

        public void GetProcesses()
        {
            string cmd = "";
            foreach (var p in Process.GetProcesses())
            {
                if (p.ProcessName.Contains("Xpra_cmd"))
                {
                    cmd = ProcCmdLine.GetCommandLineOfProcess(p);

                }


            }
        }
        public List<Ap> GetApsServer(Connection conn)
        {
            var apps = new List<Ap>();
            var r = conn.RunRemote($"ps aux |grep -v grep|grep {conn.CurrentUser}|grep \"xpra start\"");
            foreach (var line in r.Output.Split('\n'))
            {
                var m = Regex.Match(line, $@"xpra start :([0-9]+) .*--start-child=(.+) --exit-with-children");
                if (m.Success)
                {
                    var app = new Ap();
                    app.Display = int.Parse(m.Groups[1].Value);
                    app.Path = m.Groups[2].Value;                    
                    app.Status = ApStatus.IDLE;
                    apps.Add(app);
                }
            }
            return apps;
        }
        public List<Ap> GetApsLocal(Connection conn)
        {
            var apps = new List<Ap>();
            foreach (var p in Process.GetProcesses())
            {
                if (p.ProcessName.Contains("Xpra"))
                {
                    string cmdline = ProcCmdLine.GetCommandLineOfProcess(p);
                    var m = Regex.Match(cmdline, $@"attach .*ssh://.+/([0-9]+) --exit-with-children");
                    if (m.Success)
                    {
                        var app = new Ap();
                        app.Display = int.Parse(m.Groups[1].Value);
                        apps.Add(app);
                    }
                }
            }
            return apps;
        }

        
        #endregion

        

        #region Connection Management

        public ReturnBox Connect(Connection conn, IProgress<string> status)
        {
            ReturnBox r = new ReturnBox();   
            if(conn == null)
            {
                r.ConnectStatus = ConnectStatus.BAD_HOST;
                return r;
            }
                
            status?.Report("Checking server...");
            r = conn.TestHost();
            if (r.ConnectStatus != ConnectStatus.OK)
                return r;
            status?.Report("Authenticating...");
            r = conn.TestSsh();
            if (r.ConnectStatus != ConnectStatus.OK)
                return r;
            return conn.Connect();
            
        }

        public ReturnBox Disconnect(Connection conn, IProgress<string> status)
        {
            ReturnBox r = new ReturnBox();
            if (conn == null)
            {
                r.ConnectStatus = ConnectStatus.BAD_HOST;
                return r;
            }
            return conn.Disconnect();
        }
        public void CloseAllApps(Connection conn, IProgress<string> status)
        {
            foreach(var ap in conn.ApList)
            {
                status.Report($"Closing {ap.Name}...");
                conn.RunRemote($"xpra stop {ap.Display}");
            }
        }
        public ReturnBox ConnectPassword(Connection conn, string password, IProgress<string> status)
        {
            status?.Report("Connecting...");
            ReturnBox r = conn.TestPassword(password);
            if (r.ConnectStatus != ConnectStatus.OK)
                return r;
            status?.Report("Generating ssh keys...");
            r = conn.SetupSsh(password);
            if (r.ConnectStatus != ConnectStatus.OK)
                return r;
            return conn.Connect();
        }
        
        public string GetVersions()
        {
            try
            {
                string app = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                return $"App {app}";
            }
            catch (Exception ex)
            {

            }
            return "n/a";
        }

   
        

        #endregion

        


    }
}

