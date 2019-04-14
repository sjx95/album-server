using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
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

        static async Task Main(string[] args)
        {
            Debug.Assert(UsersDirectoryInfo.Exists);
            Debug.Assert(DeviceId != 0);

            logger.LogInformation($"Device ID: {DeviceId}");
            logger.LogInformation($"Your uploading page: {ServerUrl}/Upload?DeviceId={DeviceId}");

            var listRequest = WebRequest.Create($"{ServerUrl}/userdata/{DeviceId}.txt") as HttpWebRequest;
            var listResult = await listRequest.GetResponseAsync();
            var content = listResult.ToString();
            logger.LogInformation(content);
        }
    }
}
