using Newtonsoft.Json;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace xpra
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Connection : Observable
    {
        const int TIMEOUT = 20; // secs

        private string _uid;
        public string Uid
        {
            get { return _uid; }
            set
            {
                if (_uid != value)
                {
                    _uid = value;
                    foreach (var disp in DisplayList)
                        disp.Id = $"{_uid}{disp.Id}";
                    NotifyPropertyChanged();
                }
            }
        }

        string _url;
        public string Url
        {
            get { return _url; }
            set {
                _url = value;
                if (!string.IsNullOrEmpty(_url))
                {
                    string aux = _url.Replace("ssh://", "");
                    Host = aux;
                    if (aux.Contains("@"))
                    {
                        User = aux.Split('@')[0];
                        aux = aux.Split('@')[1];
                        Host = aux;
                    }
                    if (aux.Contains(":"))
                    {
                        Host = aux.Split(':')[0];
                        Port = aux.Split(':')[1];
                    }

                }
            }
        }
        public string Nickname { get; set; }
        public bool Default { get; set; }
        public List<Display> DisplayList { get; set; }
        public SshClient Ssh { get; set; }
        
        public string Error { get; set; }
        public bool IsConnected { get { return Ssh != null && Ssh.IsConnected; } }
        public string UserProfile
        {
            get
            {
                return Environment.ExpandEnvironmentVariables("%USERPROFILE%");
            }
        }

        public string AppKey
        {
            get { return $@"{UserProfile}\.ssh\id_rsa"; }
            //get { return $@"{UserProfile}\.ssh\id_rsa"; }
        }
        public string AppPubKey
        {
            get { return $@"{AppKey}.pub"; }
        }
        private string appPath;
        public string AppPath
        {
            get
            {
                if (appPath == null)
                {
                    string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                    UriBuilder uri = new UriBuilder(codeBase);
                    string path = Uri.UnescapeDataString(uri.Path);
                    appPath = Path.GetDirectoryName(path);
                }
                return appPath;
            }
        }
        private string _host;
        public string Host
        {
            get { return _host; }
            set
            {
                if (_host != value)
                {
                    _host = value;
                    NotifyPropertyChanged();
                }

            }
        }

        private string user;
        public string User
        {
            get { return user; }
            set
            {
                if (user != value)
                {
                    user = value;
                    NotifyPropertyChanged();
                }
            }
        }


        private string port;
        public string Port
        {
            get { return port; }
            set
            {
                if (port != value)
                {
                    port = value;
                    NotifyPropertyChanged();
                }
            }
        }
        public int CurrentPort
        {
            get
            {
                int port = int.Parse("0" + Port);
                if (port == 0)
                    port = 22;
                return port;
            }
        }

        public string CurrentUser
        {
            get
            {
                return string.IsNullOrEmpty(User) ? EnvironmentUser : User;
            }
        }
        public string EnvironmentUser
        {
            get { return Environment.UserName.ToLower(); }
        }

        public string ConnectButtonText => 
            IsConnected ? "DISCONNECT" : "CONNECT";

        public Connection()
        {
            DisplayList = new List<Display>();
        }
        public Display GetDisplay(string id)
        {
            return DisplayList.Where(x => x.Id == id).FirstOrDefault();
        }
        public void AddDisplay(Display d)
        {
            DisplayList.Add(d);            
        }
        public int ApCount()
        {
            int total = 0;
            foreach (var s in DisplayList)
                total += s.ApList.Count;
            return total;
        }
        #region Run Methods

        public ReturnBox RunLocal(string cmd)
        {
            // 2 secs slower
            return RunLocal("cmd.exe", "/C " + cmd);
        }

        public ReturnBox RunLocal(string cmd, string args, bool wait=true,
            int timeout_secs = 30)
        {
            Logger.Log($"Running local command: {cmd} {args}");
            ReturnBox r = new ReturnBox();
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                FileName = cmd,
                Arguments = args
            };
            process.StartInfo = startInfo;
            try
            {
                process.Start();
                if (wait)
                {
                    process.WaitForExit(timeout_secs * 1000);
                    r.Output = process.StandardOutput.ReadToEnd();
                    r.Error = process.StandardError.ReadToEnd();
                    r.ExitCode = process.ExitCode;
                    r.Success = r.ExitCode == 0;
                }
                else
                {
                    r.Success = true;
                }
            }
            catch(Exception ex)
            {
                r.Success = false;
                r.Error = ex.Message;
            }
            return r;
        }

        public ReturnBox RunRemote(string cmd, int timeout_secs = 3600)
        {
            ReturnBox r = new ReturnBox();
            if (IsConnected)
            {
                try
                {
                    SshCommand command = Ssh.CreateCommand(cmd);
                    command.CommandTimeout = TimeSpan.FromSeconds(timeout_secs);
                    r.Output = command.Execute().Trim();
                    r.Error = command.Error.Trim();
                    r.ExitCode = command.ExitStatus;
                }
                catch (Exception ex)
                {
                    r.Error = ex.Message;
                }
            }
            r.Success = r.ExitCode == 0;
            return r;
        }

        #endregion
        public ReturnBox Connect()
        {
            ReturnBox rb = new ReturnBox();
            try
            {
                var pk = new PrivateKeyFile(AppKey);
                var keyFiles = new[] { pk };
                Ssh = new SshClient(Host, CurrentPort, CurrentUser, keyFiles);
                Ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                Ssh.Connect();
                rb.Success = true;
                rb.ConnectStatus = ConnectStatus.OK;
                rb.Connection = this;
                
                UpdateItemStatus();

            }
            catch (SshAuthenticationException ex)
            {
                // bad key
                rb.Error = ex.Message;
                rb.ConnectStatus = ConnectStatus.BAD_KEY;
            }
            catch (Exception ex)
            {
                rb.Error = ex.Message;
                rb.ConnectStatus = ConnectStatus.BAD_HOST;
            }
            return rb;
        }
        public ReturnBox Disconnect()
        {
            ReturnBox rb = new ReturnBox();
            try
            {
                Ssh.Disconnect();
                rb.Success = true;
                rb.ConnectStatus = ConnectStatus.OK;
                rb.Connection = this;
                UpdateItemStatus();
            }
            catch (Exception ex)
            {
                rb.Error = ex.Message;
                rb.ConnectStatus = ConnectStatus.BAD_HOST;
            }
            return rb;
        }

        private void UpdateItemStatus()
        {
            NotifyPropertyChanged("IsConnected");
            NotifyPropertyChanged("ConnectButtonText");

            foreach (var d in DisplayList)
                d.StatusChanged();
        }
        #region SSH Management

        public ReturnBox TestHost()
        {
            ReturnBox r = new ReturnBox();
            try
            {

                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(Host, CurrentPort, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                    if (!success)
                    {
                        throw new Exception("Timeout. Server unknown or does not respond.");
                    }
                    else
                    {
                        if (client.Connected)
                        {
                            r.ConnectStatus = ConnectStatus.OK;
                            r.Success = true;
                        }
                    }
                    client.EndConnect(result);
                }
            }
            catch (Exception ex)
            {
                r.ConnectStatus = ConnectStatus.BAD_HOST;
                r.Error = ex.Message;
            }

            return r;
        }
        public ReturnBox TestPassword(string password)
        {
            ReturnBox r = new ReturnBox();

            if (string.IsNullOrEmpty(password))
            {
                r.ConnectStatus = ConnectStatus.BAD_PASSWORD;
                r.Error = "Empty password";
                return r;
            }
            try
            {
                SshClient client = new SshClient(Host, CurrentPort, CurrentUser, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(TIMEOUT);
                client.Connect();
                client.Disconnect();
                r.ConnectStatus = ConnectStatus.OK;
            }
            catch (Exception ex)
            {
                r.Error = string.Format($"Failed to connect to { CurrentUser}@{Host}:{CurrentPort}.\nError: {ex.Message}");
                if (ex is SshAuthenticationException)
                {
                    r.ConnectStatus = ConnectStatus.BAD_PASSWORD;
                }
                else if (ex is SocketException)
                {
                    r.ConnectStatus = ConnectStatus.BAD_HOST;
                }
                else
                {

                }

            }
            return r;
        }
        public ReturnBox TestSsh()
        {
            ReturnBox r = new ReturnBox();




            if (!File.Exists(AppKey))
            {
                r.ConnectStatus = ConnectStatus.BAD_KEY;
                r.Error = String.Format($"Password is required to connnect to {CurrentUser}@{Host}:{CurrentPort}.\nSSH keys will be generated and used in future conections.");               return r;
            }
            try
            {
                r.ConnectStatus = ConnectStatus.UNKNOWN;

                var pk = new PrivateKeyFile(AppKey);
                var keyFiles = new[] { pk };
                SshClient client = new SshClient(Host, CurrentPort, CurrentUser, keyFiles);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(TIMEOUT);
                client.Connect();
                client.Disconnect();
                r.ConnectStatus = ConnectStatus.OK;
                r.Success = true;
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
                if (ex is SshAuthenticationException)
                {
                    r.ConnectStatus = ConnectStatus.BAD_KEY;
                }
                else if (ex is SocketException ||
                    ex is SshConnectionException ||
                    ex is InvalidOperationException ||
                    ex.Message.Contains("milliseconds"))
                {
                    r.Error = "Host does not respond";
                    r.ConnectStatus = ConnectStatus.BAD_HOST;
                }
                else if (ex.Message.Contains("OPENSSH"))
                {
                    // openssh keys not supported by ssh.net yet
                    string args = $"-i \"{AppKey}\" -p {port} -oPasswordAuthentication=no -oStrictHostKeyChecking=no -oUserKnownHostsFile=/dev/null -oBatchMode=yes -oConnectTimeout={TIMEOUT} { CurrentUser}@{Host} \"echo ok\"";
                    var r1 = RunLocal("ssh.exe", args, true, TIMEOUT);
                    var ok = r1.Output.Trim() == "ok";
                    if (ok)
                    {
                        r.ConnectStatus = ConnectStatus.OK;
                        r.Error = "";
                        r.Success = true;
                    }
                    else
                    {
                        r.ConnectStatus = ConnectStatus.BAD_KEY;
                        r.Error = r1.Error;
                    }
                }
            }
            return r;
        }
        public ReturnBox SetupSsh(string password)
        {
            ReturnBox r = new ReturnBox();
            try
            {
                string pubkey = "";
                if (File.Exists(AppKey) && File.Exists(AppPubKey))
                {
                    pubkey = File.ReadAllText(AppPubKey);
                }
                else
                {
                    pubkey = GenerateKeys();
                }

                SshClient client = new SshClient(Host, CurrentPort, CurrentUser, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(TIMEOUT);
                client.Connect();
                string cmd = "";
                cmd = $"exec sh -c \"cd; mkdir -p .ssh; echo '{pubkey}' >> .ssh/authorized_keys; chmod 700 .ssh; chmod 644 .ssh/authorized_keys\"";
                SshCommand command = client.CreateCommand(cmd);
                command.CommandTimeout = TimeSpan.FromSeconds(TIMEOUT);
                r.Output = command.Execute();
                r.Error = command.Error;
                r.ExitCode = command.ExitStatus;

            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
                return r;
            }

            r = TestSsh();
            if (r.ConnectStatus != ConnectStatus.OK)
                return r;

            return r;
        }
        string GenerateKeys()
        {
            string pubkey = "";
            try
            {
                string dotssh = $@"{UserProfile}\.ssh";
                if (!Directory.Exists(dotssh))
                    Directory.CreateDirectory(dotssh);
                if (!File.Exists(AppKey))
                {
                    ReturnBox r = RunLocal($@"""{AppPath}\ssh-keygen.exe""", $@"-N """" -f ""{AppKey}"" -m PEM");
                }
                if (File.Exists(AppPubKey))
                {
                    pubkey = File.ReadAllText(AppPubKey).Trim();
                }
                else
                {
                    ReturnBox r = RunLocal($@"""{AppPath}\ssh-keygen.exe""", $@"-y -f ""{AppKey}""");
                    pubkey = r.Output;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error generating keys: " + ex.Message);
            }
            return pubkey;

        }

        #endregion

        public override string ToString()
        {
            return $"{ CurrentUser }@{Host}:{CurrentPort}"; ;
        }
    }

    //public class ConnectionList : List<Connection>
    //{
    //    public ConnectionList() { }
    //}
}
