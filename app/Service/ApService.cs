#pragma warning disable CS0168
using Microsoft.Win32;
using Newtonsoft.Json;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.ServiceProcess;
using System.Text.RegularExpressions;

namespace xpra
{

    public class ApService 
    {
        #region Properties

        const int TIMEOUT = 20; // secs
        public SshClient Ssh { get; set; }
        public SftpClient Sftp { get; set; }
        public string Error { get; set; }
        public bool Connected { get { return Ssh != null && Ssh.IsConnected; } }
        public List<Ap> Aps { get; } = new List<Ap>();
        
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

        public ApService()
        {

        }

        #region Serialization

        public void UpdateAps(Settings settings)
        {
            foreach (var ap in settings.Aps.Values)
                Aps.Add(ap);
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


        //public List<Drive> LoadSettingsAps()
        //{
        //    string filename = LocalAppData + "\\settings.xml";
        //    List<Drive> Aps = new List<Drive>();

        //    if (File.Exists(filename))
        //    {
        //        try
        //        {
        //            using (Stream fileStream = File.Open(filename, FileMode.Open))
        //            {
        //                var ds = new DataContractSerializer(typeof(List<Drive>));
        //                Aps = (List<Drive>)ds.ReadObject(fileStream);
        //            }
        //        }
        //        catch { }
        //    }
        //    return Aps;
        //}
        //public void SaveSettingsAps(List<Drive> Aps)
        //{
        //    try
        //    {
        //        string filename = LocalAppData + "\\settings.xml";
        //        DataContractSerializer ds = new DataContractSerializer(typeof(List<Drive>));
        //        var settings = new XmlWriterSettings { Indent = true };
        //        using (var w = XmlWriter.Create(filename, settings))
        //            ds.WriteObject(w, Aps);
        //    }
        //    catch { }
        //}


        //public void SaveSettingsAps(List<Drive> Aps)
        //{
        //    try
        //    {
        //        using (MemoryStream ms = new MemoryStream())
        //        {
        //            BinaryFormatter bf = new BinaryFormatter();
        //            bf.Serialize(ms, Aps);
        //            ms.Position = 0;
        //            byte[] buffer = new byte[(int)ms.Length];
        //            ms.Read(buffer, 0, buffer.Length);
        //            Properties.Settings.Default.Aps = Convert.ToBase64String(buffer);
        //            Properties.Settings.Default.Save();
        //        }
        //    }
        //    catch (Exception ex )
        //    {

        //    }
        //}

        //private List<Drive> LoadSettingsAps()
        //{
        //    List<Drive> Aps = new List<Drive>();
        //    try
        //    {
        //        using (MemoryStream ms = new MemoryStream(
        //            Convert.FromBase64String(Properties.Settings.Default.Aps)))
        //        {
        //            BinaryFormatter bf = new BinaryFormatter();
        //            if (ms.Length > 0)
        //                Aps = (List<Drive>)bf.Deserialize(ms);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //    }
        //    return Aps;
        //}

        #endregion

        #region Core Methods

        public bool Connect(string host, int port, string user)
        {
            try
            {
                var pk = new PrivateKeyFile(AppKey);
                var keyFiles = new[] { pk };
                Ssh = new SshClient(host, port, user, keyFiles);
                Ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                Ssh.Connect();
                
                //Sftp = new SftpClient(host, port, user, keyFiles);
                //Sftp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                //Sftp.Connect();
            }
            catch (SshAuthenticationException ex)
            {
                // bad key
                Error = ex.Message;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            return Connected;
        }

        public ReturnBox RunLocal(string cmd)
        {
            // 2 secs slower
            return RunLocal("cmd.exe", "/C " + cmd);
        }

        public ReturnBox RunLocal(string cmd, string args, 
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
            process.Start();
            process.WaitForExit(timeout_secs * 1000);
            r.Output = process.StandardOutput.ReadToEnd();
            r.Error = process.StandardError.ReadToEnd();
            r.ExitCode = process.ExitCode;
            r.Success = r.ExitCode == 0;
            return r;
        }

        public ReturnBox RunRemote(string cmd, int timeout_secs = 3600)
        {
            ReturnBox r = new ReturnBox();
            if (Connected)
            {
                try
                {
                    SshCommand command = Ssh.CreateCommand(cmd);
                    command.CommandTimeout = TimeSpan.FromSeconds(timeout_secs);
                    r.Output = command.Execute();
                    r.Error = command.Error;
                    r.ExitCode = command.ExitStatus;
                }
                catch (Exception ex)
                {
                    r.Error = ex.Message;
                }
            }
            r.Success = r.ExitCode == 0 && String.IsNullOrEmpty(r.Error);
            return r;
        }

        public ReturnBox DownloadFile(string src, string dst)
        {
            ReturnBox r = new ReturnBox();
            if (Connected)
            {
                try
                {
                    using (Stream fs = File.Create(dst))
                    {
                        Sftp.DownloadFile(src, fs);
                    }
                    r.Success = true;
                }
                catch (Exception ex)
                {
                    r.Error = ex.Message;
                }
            }
            return r;
        }

        public ReturnBox UploadFile(string src, string dir, string filename)
        {
            ReturnBox r = new ReturnBox();
            if (Connected)
            {
                try
                {
                    using (var fs = new FileStream(src, FileMode.Open))
                    {
                        Sftp.BufferSize = 4 * 1024; // bypass Payload error large files
                        Sftp.ChangeDirectory(dir);
                        Sftp.UploadFile(fs, filename, true);
                    }
                    r.Success = true;
                }
                catch (Exception ex)
                {
                    r.Error = ex.Message;
                }
            }
            return r;
        }

        #endregion

        #region Ap Management


        public ReturnBox RunAp(string appname, IProgress<string> status)
        {
            ReturnBox r = new ReturnBox();
            var ap = Aps.Where(x => x.Name == appname).First();
            if (ap == null)
                return r;

            // run in server
            if(!Connected)
            {
                var msg = "Not connected";
                r.Error = msg;
                return r;
            }
            var cmd = $"/usr/bin/xpra start --exit-with-children --start-child={ap.Path} :{ap.Display}";
            var rb1 = RunRemote(cmd);

            // attach
            cmd = @"C:\Xpra-Client-Python3-x86_64_4.0-r26306\Xpra_cmd.exe";
            var args = $"attach ssh://san@166.87.146.140/{ap.Display} --exit-with-children --microphone=off --speaker=off --tray=no --dpi=100 --webcam=off";
            var rb2 = RunLocal(cmd, args);
            return r;
        }

        #endregion

        #region SSH Management

        public ReturnBox TestHost(string host, int port)
        {
            ReturnBox r = new ReturnBox();
            try
            {

                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
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
        ReturnBox TestPassword(string host, int port, string username, string password)
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
                SshClient client = new SshClient(host, port, username, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(TIMEOUT);
                client.Connect();
                client.Disconnect();
                r.ConnectStatus = ConnectStatus.OK;
            }
            catch (Exception ex)
            {
                r.Error = string.Format($"Failed to connect to { username}@{host}:{port}.\nError: {ex.Message}" );
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
        public ReturnBox TestSsh(string host, int port, string username)
        {
            ReturnBox r = new ReturnBox();

 
            

            if (!File.Exists(AppKey))
            {
                r.ConnectStatus = ConnectStatus.BAD_KEY;
                r.Error = String.Format($"Password is required to connnect to {username}@{host}:{port}.\nSSH keys will be generated and used in future conections.");
                return r;
            }
            try
            {
                r.ConnectStatus = ConnectStatus.UNKNOWN;

                var pk = new PrivateKeyFile(AppKey);
                var keyFiles = new[] { pk };
                SshClient client = new SshClient(host, port, username, keyFiles);
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
                    string args = $"-i \"{AppKey}\" -p {port} -oPasswordAuthentication=no -oStrictHostKeyChecking=no -oUserKnownHostsFile=/dev/null -oBatchMode=yes -oConnectTimeout={TIMEOUT} { username}@{host} \"echo ok\"";
                    var r1 = RunLocal("ssh.exe", args, TIMEOUT);
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
        public ReturnBox SetupSsh(string host, int port, string username, string password)
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
                
                SshClient client = new SshClient(host, port, username, password);
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

            r = TestSsh(host, port, username);
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
                    ReturnBox r = RunLocal($@"""{AppPath}\ssh-keygen.exe""", $@"-N """" -f ""{AppKey}""");
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

        #region Connection Management

        //public ReturnBox Connect(string host, int port, string username, IProgress<string> status)
        //{
        //    ReturnBox r = new ReturnBox();
            
        //    status?.Report("Checking server...");
        //    r = TestHost(host, port);
        //    if (r.ConnectStatus != ConnectStatus.OK)
        //        return r;
        //    status?.Report("Authenticating...");
        //    return TestSsh(host, port, username);
        //}
        public ReturnBox ConnectPassword(string host, int port, string username, string password, IProgress<string> status)
        {
            status?.Report("Connecting...");
            ReturnBox r = TestPassword(host, port, username, password);
            if (r.ConnectStatus != ConnectStatus.OK)
                return r;
            status?.Report("Generating ssh keys...");
            return SetupSsh(host, port, username, password);            
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

