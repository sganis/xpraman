#pragma warning disable CS0168
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

        
        public ObservableCollection<Connection> ConnectionList { get; } = new ObservableCollection<Connection>();
        public Connection SelectedConnection { get; set; } = new Connection();
        

        
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

        public MainService()
        {

        }

        #region Serialization

        public void UpdateFromSettings(Settings settings)
        {
            foreach (var c in settings.ConnectionList)
                ConnectionList.Add(c);
        }

        public Settings LoadSettings()
        {
            Settings settings = new Settings() {
                Filename = LocalAppData + "\\config.json" 
            };
            settings.Load();
            return settings;
        }

        public void SaveSettings(Settings settings)
        {
            try
            {
                settings.Filename = LocalAppData + "\\config.json";
                
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


        public ReturnBox RunAp(Connection conn, string appname, IProgress<string> status)
        {
            ReturnBox r = new ReturnBox();
            var ap = SelectedConnection.GetApp(appname);
            if (ap == null)
                return r;

            // run in server
            if(!conn.Connected)
            {
                var msg = "Not connected";
                r.Error = msg;
                return r;
            }
            var cmd = $"/usr/bin/xpra start --exit-with-children --start-child={ap.Path} :{ap.Display}";
            var rb1 = conn.RunRemote(cmd);

            // attach
            cmd = @"C:\Xpra-Client-Python3-x86_64_4.0-r26306\Xpra_cmd.exe";
            var args = $"attach ssh://san@166.87.146.140/{ap.Display} --exit-with-children --microphone=off --speaker=off --tray=no --dpi=100 --webcam=off";
            var rb2 = conn.RunLocal(cmd, args);
            return r;
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
        public List<Ap> GetApsServer()
        {
            return new List<Ap>();
        }
        public List<Ap> GetApsLocal()
        {
            return new List<Ap>();
        }

        public void Detach(int display)
        {
            string cmd = "";
            foreach (var p in Process.GetProcesses())
            {
                if (p.ProcessName.Contains("Xpra_cmd"))
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
            r = conn.Connect();
            if (r.ConnectStatus != ConnectStatus.OK)
                return r;
            ConnectionList.Add(conn);
            NotifyPropertyChanged("ConnectionList");
            return r;
        }
        public ReturnBox ConnectPassword(Connection conn, string password, IProgress<string> status)
        {
            status?.Report("Connecting...");
            ReturnBox r = conn.TestPassword(password);
            if (r.ConnectStatus != ConnectStatus.OK)
                return r;
            status?.Report("Generating ssh keys...");
            return conn.SetupSsh(password);            
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

