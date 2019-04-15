using System;

using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace album_client
{
    class Program
    {
        public static readonly string ServerUrl = ConfigurationManager.AppSettings["ServerUrl"];
        public static readonly int ResidenceTime = int.Parse(ConfigurationManager.AppSettings["ResidenceTime"]);
        public static readonly FileInfo AssemblyFileInfo = new FileInfo(Assembly.GetExecutingAssembly().FullName);
        public static readonly DirectoryInfo UsersDirectoryInfo = AssemblyFileInfo.Directory?.CreateSubdirectory("userdatas");
        public static readonly UInt64 DeviceId;
        public static readonly bool UseXinitInLinux = bool.Parse(ConfigurationManager.AppSettings["UseXinitInLinux"]);
        private static readonly ILogger Logger = new LoggerFactory().AddConsole().CreateLogger(typeof(Program).FullName);

        static Program()
        {
            var str = ConfigurationManager.AppSettings["DeviceId"];
            if (str != null)
            {
                DeviceId = UInt64.Parse(str);
            }
            else
            {
                var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(i =>
                        i.OperationalStatus == OperationalStatus.Up && 
                        i.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                Logger.LogDebug($"IF Name: {networkInterface.Name}");
                DeviceId = UInt64.Parse(networkInterface?.GetPhysicalAddress().ToString(), NumberStyles.HexNumber);

                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                settings.Add("DeviceId", DeviceId.ToString());
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
        }

        private static async Task<bool> UpdateListAsync()
        {
            var listRequest = WebRequest.Create($"{ServerUrl}/userdata/{DeviceId}.txt");
            var listResult = await listRequest.GetResponseAsync();
            var content = await new StreamReader(listResult.GetResponseStream()).ReadToEndAsync();

            try
            {
                var hasher = SHA1.Create();
                var newHash = hasher.ComputeHash(Encoding.Default.GetBytes(content));
                var oldHash = hasher.ComputeHash(File.ReadAllBytes($"{UsersDirectoryInfo.FullName}/list.txt"));
                if (BitConverter.ToString(oldHash) == BitConverter.ToString(newHash))
                {
                    return false;
                }
            } catch (FileNotFoundException)
            {
                Logger.LogWarning($"Local list not found.");
            }

            Logger.LogInformation($"New list: \n{content}");
            await File.WriteAllTextAsync($"{UsersDirectoryInfo.FullName}/list.txt", content);
            
            return true;
        }


        private static async Task UpdateAlbumAsync()
        {
            var files = File.ReadLines($"{UsersDirectoryInfo.FullName}/list.txt").ToList();
            foreach (var fi in UsersDirectoryInfo.EnumerateFiles().Where(fi => fi.Name != "list.txt"))
                fi.Delete();

            foreach (var fn in files)
            {
                var url = $"{ServerUrl}/userdata/{DeviceId}/{fn}";
                var request = FileWebRequest.Create(url);

                Logger.LogInformation($"Downloading: {url}");
                var response = await request.GetResponseAsync();
                var responseStream = response.GetResponseStream();

                var path = $"{UsersDirectoryInfo.FullName}/{fn}";
                var fs = new FileStream(path, FileMode.Create);
                await responseStream.CopyToAsync(fs);
                fs.Close();
            }

            Logger.LogInformation($"{files.Count} file(s) downloaded.");
        }

        static async Task Main(string[] args)
        {
            Debug.Assert(UsersDirectoryInfo.Exists);
            Debug.Assert(DeviceId != 0);

            Logger.LogInformation($"Device ID: {DeviceId}");
            Logger.LogInformation($"Your uploading page: {ServerUrl}/Upload?DeviceId={DeviceId}");

            var startInfo = new ProcessStartInfo {WorkingDirectory = $"{UsersDirectoryInfo.FullName}", };
            var mpvArgs = $"--playlist=list.txt " +
                       $"--image-display-duration={ResidenceTime} " +
                       $"--fs ";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && UseXinitInLinux)
            {
                mpvArgs += "--loop " + // TODO Check mpv version
                           "--geometry=100%x100% ";
                var findMpv = new Process { StartInfo = new ProcessStartInfo("which", "mpv") { RedirectStandardOutput = true, UseShellExecute = false } };
                findMpv.Start();
                findMpv.WaitForExit();
                var mpvPath = findMpv.StandardOutput.ReadLine();
                if (string.IsNullOrWhiteSpace(mpvPath))
                {
                    Logger.LogError("mpv not found in system, install it by \"apt install mpv\" (Debian).");
                    throw new FileNotFoundException("mpv not found in system.");
                }
                startInfo.FileName = "xinit";
                startInfo.Arguments = $"{mpvPath} {mpvArgs}";
            }
            else
            {
                mpvArgs += "--loop-playlist ";
                startInfo.FileName = "mpv";
                startInfo.Arguments = mpvArgs;
            }
            var projector = new Process {StartInfo = startInfo};
            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => projector.Kill();
            Console.CancelKeyPress += (sender, eventArgs) => projector.Kill();
            projector.Start();

            while (true)
            {
                try
                {
                    if (await UpdateListAsync())
                    {
                        if (!projector.HasExited)
                        {
                            projector.Kill();
                        }
                        projector.Close();

                        Logger.LogInformation("Start album reloading due to list updated.");
                        await UpdateAlbumAsync();

                        projector.Start();
                    }
                } catch (WebException e)
                {
                    Logger.LogWarning(e.ToString());
                }
                await Task.Delay(2000);
            }
        }
    }
}
