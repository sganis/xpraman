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
using System.Windows.Controls;

namespace xpra
{

    public class MainService : Observable
    {
        #region Properties

        string m_xpra_local;

        Settings m_settings;

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
            m_settings = settings;

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
            ap.Status = Status.STARTING;
            status.Report($"Starting {ap.Name}...");
            var cmd = $"export DISPLAY=:{ap.DisplayId}; \"{ap.Path}\" >/dev/null 2>&1 & echo $!";
            var r = conn.RunRemote(cmd);
            if (r.Success)
            {
                ap.Status = Status.ACTIVE;
                // fixme, get pgid, not pid
                var pid = int.Parse(r.Output);
                //ap.AddPgid(pid);
                status.Report($"{ap.Name} stated.");
            }
            return r;
        }
        public ReturnBox CloseAp(Connection conn, Ap ap, IProgress<string> status)
        {
            if (ap.ProcessGroupIds.Count > 0)
            {

                ReturnBox r = null;
                foreach(var pgid in ap.ProcessGroupIds)
                {
                    r = conn.RunRemote($"kill -- -{pgid}");
                    //if (!r.Success)
                    //    break;
                    
                }
                if (r.Success)
                {
                    ap.Status = Status.STOPPED;
                    status.Report($"{ap.Name} closed.");
                }
                return r;
            }
            else
            {
                return new ReturnBox
                {
                    Success = false,
                    Error = "Unknown application process ID"
                };
            }
        }
            //status.Rep
        public ReturnBox XpraStart(Connection conn, Display display, IProgress<string> status)
        {
            ReturnBox r = new ReturnBox();
            display.Status = Status.STARTING;
            var cmd = $"xpra start :{display.Id} {m_settings.XpraServerArgs} ";
            r = conn.RunRemote(cmd);
            if (r.Success)
            {
                display.Status = Status.DETACHED;    
            } 
            else
            {
                status.Report($"Xpra start error: {r.Error}");
            }
            return r;
        }

        public ReturnBox XpraStop(Connection conn, Display display, IProgress<string> status)
        {
            ReturnBox r = new ReturnBox();
            display.Status = Status.STOPPING;
            var cmd = $"xpra stop :{display.Id}";
            System.Threading.Thread.Sleep(2000);
            r = conn.RunRemote(cmd);
            if (r.Success)
                display.Status = Status.STOPPED;
            return r;
        }

        public ReturnBox XpraAttach(Connection conn, Display display, IProgress<string> status)
        {
            display.Status = Status.ATTACHING;
            //var opengl = "--opengl";
            System.Threading.Thread.Sleep(2000);
            var opengl = "";
            var cmd = m_xpra_local;
            var param = 
                "--microphone=off --speaker=off --tray=no " +
                "--webcam=no --idle-timeout=0 --cursors=yes --compress=0 " +
                $"{ opengl }";
            var args = $"attach ssh://{conn.CurrentUser}@{conn.Host}:{conn.CurrentPort}/{display.Id} {param} {m_settings.XpraClientArgs}";
            var r = conn.RunLocal(cmd, args, false);
            if (r.Success)
                display.Status = Status.ACTIVE;
            return r;
        }
        public ReturnBox Detach(Display display, IProgress<string> status)
        {
            display.Status = Status.DETACHING;
            var r = new ReturnBox();
            string cmd = "";
            System.Threading.Thread.Sleep(2000);
            foreach (var p in Process.GetProcesses())
            {
                if (p.ProcessName.Contains("Xpra"))
                {
                    cmd = ProcCmdLine.GetCommandLineOfProcess(p);
                    if (Regex.IsMatch(cmd, $@"attach ssh://.+/{display.Id} "))
                    {
                        p.Kill();
                        r.Success = true;
                        break;
                    }
                }
            }
            display.Status = Status.DETACHED;
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
        public List<Ap> GetApsServer(Connection conn)
        {
            // get server xpra sessions
            var r = conn.RunRemote($"xpra list |grep -o \"LIVE session at :[0-9]\\+\" | awk '{{print $4}}'");
            var apps = new Dictionary<string, Ap>();
            var displays = new Dictionary<int, Display>();

            foreach (var line in r.Output.Split('\n'))
            {
                var disp_str = line.Trim();
                if (String.IsNullOrEmpty(disp_str) || !disp_str.Contains(":"))
                    continue;
                int displayId = int.Parse(disp_str.Split(':')[1]);
                Display disp = null;
                if (!displays.ContainsKey(displayId))
                {
                    disp = new Display(displayId);
                    displays[displayId] = disp;
                }
                disp = displays[displayId];
                // get pid,pgid,command
                r = conn.RunRemote($"ps exo pid,pgid,args |grep \"DISPLAY=:{displayId}\" |grep -v grep |awk '{{print $1\",\"$2\",\"$3}}'");
                
                foreach (var pid_path in r.Output.Split('\n'))
                {
                    var aux = pid_path.Split(',');
                    if (aux.Length < 3)
                        continue;
                    var process_path = aux[2];
                    Ap app = null;
                    if (!apps.ContainsKey(process_path))
                    {
                        app = new Ap(disp);
                        app.Process = process_path;
                        apps[process_path] = app;
                    }
                    app = apps[process_path];
                    app.AddPgid(int.Parse(aux[1]));
                    app.Status = Status.DETACHED;
                }
            }
            return apps.Values.ToList();
        }
        public List<int> GetDisplaysLocal()
        {
            var displays = new List<int>();
            foreach (var p in Process.GetProcesses())
            {
                if (p.ProcessName.Contains("Xpra"))
                {
                    string cmdline = ProcCmdLine.GetCommandLineOfProcess(p);
                    var m = Regex.Match(cmdline, $@"attach .*ssh://.+/([0-9]+)");
                    if (m.Success)
                    {
                        displays.Add(int.Parse(m.Groups[1].Value));
                    }
                }
            }
            return displays;
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
            foreach (var display in conn.DisplayList)
            {
                XpraStop(conn, display, status);
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

