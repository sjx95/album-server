using Microsoft.Extensions.Logging;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace album_client
{
    class Projector : IDisposable
    {
        private static readonly ILogger Logger = new LoggerFactory().AddConsole().CreateLogger(typeof(Program).FullName);

        Process xinit, mpv, music;

        public Projector()
        {
            if (Program.UseXinitInLinux && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                xinit = new Process
                {
                    StartInfo = new ProcessStartInfo("Xorg")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };
            }

            var mpvArgs = $"--no-input-cursor " + 
                $"--playlist=list.txt " +
                $"--image-display-duration={Program.ResidenceTime} " +
                $"--fs ";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && Program.UseXinitInLinux)
            {
                mpvArgs += "--loop " + // TODO Check mpv version
                           "--geometry=100%x100% ";
            }
            else
            {
                mpvArgs += "--loop-playlist ";
            }

            mpv = new Process() {
                StartInfo = new ProcessStartInfo("mpv", mpvArgs)
                {
                    WorkingDirectory = $"{Program.UsersDirectoryInfo.FullName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            if (Program.UseXinitInLinux && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                mpv.StartInfo.EnvironmentVariables["DISPLAY"] = ":0";
            }

            var musicUri = ConfigurationManager.AppSettings["MusicUri"];
            music = new Process
            {
                StartInfo = new ProcessStartInfo("mpv", $"{musicUri} --loop --no-video")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
        }

        public void Start()
        {
            if (xinit != null)
            {
                xinit.Start();
            }
            mpv.Start();
            if (music != null)
            {
                music.Start();
            }
        }

        public void Dispose()
        {
            Stop();
        }

        public void Stop()
        {
            if (!mpv.HasExited)
            {
                mpv.CloseMainWindow();
                if (!mpv.WaitForExit(1000))
                    mpv.Kill();
                mpv.Close();
            }

            if (xinit != null && !xinit.HasExited)
            {
                var killer = Process.Start("kill", $"{xinit.Id}");
                killer.WaitForExit();
                if (killer.ExitCode != 0)
                {
                    xinit.Kill();
                }
                xinit.Close();
            }

            if (music != null && !music.HasExited)
            {
                music.CloseMainWindow();
                if (!music.WaitForExit(1000))
                    music.Kill();
                music.Close();
            }
        }
    }
}
