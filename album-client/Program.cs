using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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

namespace album_client
{
    class Program
    {
        public static readonly string ServerUrl = ConfigurationManager.AppSettings["ServerUrl"];
        public static readonly int ResidenceTime = int.Parse(ConfigurationManager.AppSettings["ResidenceTime"]);
        public static readonly FileInfo AssemblyFileInfo = new FileInfo(Assembly.GetExecutingAssembly().FullName);
        public static readonly DirectoryInfo UsersDirectoryInfo = AssemblyFileInfo.Directory?.CreateSubdirectory("userdatas");
        public static readonly UInt64 DeviceId;
        private static readonly ILogger logger = new LoggerFactory().AddConsole().CreateLogger(typeof(Program).FullName);

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

                logger.LogDebug($"IF Name: {networkInterface.Name}");
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
            } catch (FileNotFoundException e)
            {
                logger.LogWarning($"Local list not found.");
            }

            logger.LogInformation($"New list: \n{content}");
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

                logger.LogInformation($"Downloading: {url}");
                var response = await request.GetResponseAsync();
                var content = new StreamReader(response.GetResponseStream()).ReadToEnd();

                var path = $"{UsersDirectoryInfo.FullName}/{fn}";
                var fs = new StreamWriter(fn);
                fs.Write(content);
                fs.Close();
            }

            logger.LogInformation($"{files.Count} file(s) downloaded.");
        }

        static async Task Main(string[] args)
        {
            Debug.Assert(UsersDirectoryInfo.Exists);
            Debug.Assert(DeviceId != 0);

            logger.LogInformation($"Device ID: {DeviceId}");
            logger.LogInformation($"Your uploading page: {ServerUrl}/Upload?DeviceId={DeviceId}");


            while (true)
            {
                try
                {
                    if (await UpdateListAsync())
                    {
                        logger.LogInformation("Start album reloading due to list updated.");
                        await UpdateAlbumAsync();
                    }

                    await Task.Delay(2000);

                } catch (WebException e)
                {
                    logger.LogWarning(e.ToString());
                }
            }
        }
    }
}
