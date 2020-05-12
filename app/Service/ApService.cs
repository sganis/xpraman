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
        public List<Ap> ApList
        {
            get
            {
                //return Aps.Where(x => x.Status != Apstatus.FREE && x.IsGoldDrive == true).ToList();
                return Aps;
            }
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
                    localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\golddrive";
                return localAppData;
            }
        }

        #endregion

        public ApService()
        {

        }

        #region Serialization


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

        //public bool Connect(string host, int port, string user, string pkey)
        //{
        //    try
        //    {
        //        var pk = new PrivateKeyFile(pkey);
        //        var keyFiles = new[] { pk };
        //        Ssh = new SshClient(host, port, user, keyFiles);
        //        Ssh.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
        //        Ssh.Connect();
        //        //Sftp = new SftpClient(host, port, user, keyFiles);
        //        //Sftp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
        //        //Sftp.Connect();
        //    }
        //    catch (Renci.SshNet.Common.SshAuthenticationException ex)
        //    {
        //        // bad key
        //        Error = ex.Message;
        //    }
        //    catch (Exception ex)
        //    {
        //        Error = ex.Message;
        //    }
        //    return Connected;
        //}

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

        //public ReturnBox DownloadFile(string src, string dst)
        //{
        //    ReturnBox r = new ReturnBox();
        //    if (Connected)
        //    {
        //        try
        //        {
        //            using (Stream fs = File.Create(dst))
        //            {
        //                Sftp.DownloadFile(src, fs);
        //            }
        //            r.Success = true;
        //        }
        //        catch (Exception ex)
        //        {
        //            r.Error = ex.Message;
        //        }
        //    }
        //    return r;
        //}

        //public ReturnBox UploadFile(string src, string dir, string filename)
        //{
        //    ReturnBox r = new ReturnBox();
        //    if (Connected)
        //    {
        //        try
        //        {
        //            using (var fs = new FileStream(src, FileMode.Open))
        //            {
        //                Sftp.BufferSize = 4 * 1024; // bypass Payload error large files
        //                Sftp.ChangeDirectory(dir);
        //                Sftp.UploadFile(fs, filename, true);
        //            }
        //            r.Success = true;
        //        }
        //        catch (Exception ex)
        //        {
        //            r.Error = ex.Message;
        //        }
        //    }
        //    return r;
        //}

        #endregion

        #region Local Ap Management


        public bool CheckIfDriveWorks(Ap drive)
        {
            // Fist try DriveInfo
            var info = new DriveInfo(drive.Letter);
            bool ok = false;
            try {
                ok = info.AvailableFreeSpace >= 0;
            } catch (IOException) {
                return false;
            }
            return ok;
            //if (works) {
            //    int epoch = (int)(DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds;
            //    string tempfile = $@"{ drive.Name }\tmp\{drive.User}@{drive.Host}.{epoch}";
            //    var r = RunLocal("type nul > " + tempfile);
            //    if (r.ExitCode == 0) {
            //        RunLocal("del " + tempfile);
            //        return true;
            //    }
            //}
            //return false;
        }
        public string GetExplorerDriveLabel(Ap drive)
        {
            try
            {
                string key = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2\{drive.RegistryMountPoint2}";
                RegistryKey k = Registry.CurrentUser.OpenSubKey(key);
                if (k != null)
                    return k.GetValue("_LabelFromReg")?.ToString();
            }
            catch (Exception ex)
            {

            }
            return "";
        }
        public void SetExplorerDriveLabel(Ap drive)
        {
            if (String.IsNullOrEmpty(drive.Label))
                return;
            try
            {
                string key = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2\{drive.RegistryMountPoint2}";
                RegistryKey k = Registry.CurrentUser.CreateSubKey(key);
                if (k != null)
                    k.SetValue("_LabelFromReg", drive.Label, RegistryValueKind.String);
            }
            catch (Exception ex)
            {

            }
        }
        public void CleanExplorerDriveLabel(Ap drive)
        {
            if (String.IsNullOrEmpty(drive.RegistryMountPoint2))
                return;
            try
            {
                string key = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2\{drive.RegistryMountPoint2}";
                Registry.CurrentUser.DeleteSubKey(key);
            }
            catch { }
        }
        public void SetDriveIcon(Ap drive, string icoPath)
        {
            try
            {
                string key = $@"Software\Classes\Applications\Explorer.exe\Aps\{drive.Letter}\DefaultIcon";
                RegistryKey k = Registry.CurrentUser.CreateSubKey(key);
                if (k != null)
                    k.SetValue("", icoPath, RegistryValueKind.String);
            }
            catch (Exception ex)
            {

            }
        }


        #endregion

        #region SSH Management

        public ReturnBox TestHost(Ap drive)
        {
            ReturnBox r = new ReturnBox();
            try
            {

                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(drive.Host, 
                        drive.CurrentPort, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                    if (!success)
                    {
                        throw new Exception("Timeout. Server unknown or does not respond.");
                    }
                    else
                    {
                        if (client.Connected)
                        {
                            r.MountStatus = MountStatus.OK;
                            r.Success = true;
                        }
                    }
                    client.EndConnect(result);
                }
            }
            catch (Exception ex)
            {
                r.MountStatus = MountStatus.BAD_HOST;
                r.Error = ex.Message;
            }

            return r;
        }
        ReturnBox TestPassword(Ap drive, string password)
        {
            ReturnBox r = new ReturnBox();

            if (string.IsNullOrEmpty(password))
            {
                r.MountStatus = MountStatus.BAD_PASSWORD;
                r.Error = "Empty password";
                return r;
            }
            try
            {
                SshClient client = new SshClient(drive.Host, drive.CurrentPort, drive.CurrentUser, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(TIMEOUT);
                client.Connect();
                client.Disconnect();
                r.MountStatus = MountStatus.OK;
            }
            catch (Exception ex)
            {
                r.Error = string.Format($"Failed to connect to { drive.CurrentUser}@{drive.Host}:{drive.CurrentPort}.\nError: {ex.Message}" );
                if (ex is SshAuthenticationException)
                {
                    r.MountStatus = MountStatus.BAD_PASSWORD;
                }
                else if (ex is SocketException)
                {
                    r.MountStatus = MountStatus.BAD_HOST;
                }
                else
                {

                }

            }
            return r;
        }
        public ReturnBox TestSsh(Ap drive)
        {
            ReturnBox r = new ReturnBox();

 
            

            if (!File.Exists(drive.AppKey))
            {
                r.MountStatus = MountStatus.BAD_KEY;
                r.Error = String.Format($"Password is required to connnect to {drive.CurrentUser}@{drive.Host}:{drive.CurrentPort}.\nSSH keys will be generated and used in future conections.");
                return r;
            }
            try
            {
                r.MountStatus = MountStatus.UNKNOWN;

                var pk = new PrivateKeyFile(drive.AppKey);
                var keyFiles = new[] { pk };
                SshClient client = new SshClient(drive.Host, drive.CurrentPort, drive.CurrentUser, keyFiles);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(TIMEOUT);
                client.Connect();
                client.Disconnect();
                r.MountStatus = MountStatus.OK;
                r.Success = true;
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
                if (ex is SshAuthenticationException)
                {
                    r.MountStatus = MountStatus.BAD_KEY;
                }
                else if (ex is SocketException ||
                    ex is SshConnectionException ||
                    ex is InvalidOperationException ||
                    ex.Message.Contains("milliseconds"))
                {
                    r.Error = "Host does not respond";
                    r.MountStatus = MountStatus.BAD_HOST;
                }
                else if (ex.Message.Contains("OPENSSH"))
                {
                    // openssh keys not supported by ssh.net yet
                    string args = $"-i \"{drive.AppKey}\" -p {drive.CurrentPort} -oPasswordAuthentication=no -oStrictHostKeyChecking=no -oUserKnownHostsFile=/dev/null -oBatchMode=yes -oConnectTimeout={TIMEOUT} { drive.CurrentUser}@{drive.Host} \"echo ok\"";
                    var r1 = RunLocal("ssh.exe", args, TIMEOUT);
                    var ok = r1.Output.Trim() == "ok";
                    if (ok)
                    {
                        r.MountStatus = MountStatus.OK;
                        r.Error = "";
                        r.Success = true;
                    }
                    else
                    {
                        r.MountStatus = MountStatus.BAD_KEY;
                        r.Error = r1.Error;
                    }
                }
            }
            return r;
        }
        public ReturnBox SetupSsh(Ap drive, string password)
        {
            ReturnBox r = new ReturnBox();
            try
            {
                string pubkey = "";
                if (File.Exists(drive.AppKey) && File.Exists(drive.AppPubKey))
                {
                    pubkey = File.ReadAllText(drive.AppPubKey);
                }
                else
                {
                    pubkey = GenerateKeys(drive);
                }
                
                SshClient client = new SshClient(drive.Host, drive.CurrentPort, drive.CurrentUser, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(TIMEOUT);
                client.Connect();
                string cmd = "";
                bool linux = !client.ConnectionInfo.ServerVersion.ToLower().Contains("windows");
                if (linux)
                {
                    cmd = $"exec sh -c \"cd; mkdir -p .ssh; echo '{pubkey}' >> .ssh/authorized_keys; chmod 700 .ssh; chmod 644 .ssh/authorized_keys\"";
                }
                else
                {
                    ////cmd = "if not exists .ssh mkdir .ssh && ";
                    //cmd = $"echo {pubkey.Trim()} >> .ssh\\authorized_keys && ";
                    //cmd += $"icacls .ssh\\authorized_keys /inheritance:r && ";
                    //cmd += $"icacls .ssh\\authorized_keys /grant {drive.User}:f &&";
                    //cmd += $"icacls .ssh\\authorized_keys /grant SYSTEM:f";
                }
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

            r = TestSsh(drive);
            if (r.MountStatus != MountStatus.OK)
                return r;

            return r;
        }
         string GenerateKeys(Ap drive)
         {
            string pubkey = "";
            try
            {
                string dotssh = $@"{drive.UserProfile}\.ssh";
                if (!Directory.Exists(dotssh))
                    Directory.CreateDirectory(dotssh);
                if (!File.Exists(drive.AppKey))
                {
                    ReturnBox r = RunLocal($@"""{AppPath}\ssh-keygen.exe""", $@"-N """" -f ""{drive.AppKey}""");
                }
                if (File.Exists(drive.AppPubKey))
                {
                    pubkey = File.ReadAllText(drive.AppPubKey).Trim();
                }
                else
                {
                    ReturnBox r = RunLocal($@"""{AppPath}\ssh-keygen.exe""", $@"-y -f ""{drive.AppKey}""");
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

        #region Mount Management

        public ReturnBox Connect(Ap drive, IProgress<string> status)
        {
            ReturnBox r = new ReturnBox();
            
            status?.Report("Checking server...");
            r = TestHost(drive);
            if (r.MountStatus != MountStatus.OK)
                return r;
            status?.Report("Authenticating...");
            return TestSsh(drive);
        }
        public ReturnBox ConnectPassword(Ap drive, string password, IProgress<string> status)
        {
            status?.Report("Connecting...");
            ReturnBox r = TestPassword(drive, password);
            if (r.MountStatus != MountStatus.OK)
                return r;
            status?.Report("Generating ssh keys...");
            return SetupSsh(drive, password);            
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

        public ReturnBox Mount(Ap drive)
        {
            ReturnBox r = RunLocal("net.exe", $"use { drive.Name } { drive.Remote } /persistent:yes");
            //if (!r.Success)
            //{
            //    r.MountStatus = MountStatus.UNKNOWN;
            //    r.Drive = drive;
            //    return r;
            //}
            //SetExplorerDriveLabel(drive);
            //SetDriveIcon(drive, $@"{ AppPath }\golddrive.ico");
            //Settings settings = LoadSettings();
            //settings.AddDrive(drive);
            //SaveSettings(settings);
            //UpdateAps(settings);
            //r.MountStatus = MountStatus.OK;
            //r.Apstatus = Apstatus.CONNECTED;
            //r.Drive = drive;
            return r;
        }

      
        

        #endregion

        public string GetUid(string user)
        {
            return RunRemote($"id -u {user}").Output;
        }


    }
}

