using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace album_server.Pages
{
    [IgnoreAntiforgeryToken(Order = 1010)]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public class UploadModel : PageModel
    {
        [BindProperty(SupportsGet = true)] public UInt64 DeviceId { get; set; } 
        [BindProperty] public FormFileCollection Pictures { get; set; }
        [BindProperty] public bool AppendMode { get; set; }

        public HashSet<string> FileSet = new HashSet<string>();
        public HashSet<string> ConflictSet = new HashSet<string>();

        private static ConcurrentDictionary<UInt64, DateTime> locks = new ConcurrentDictionary<UInt64, DateTime>();
        
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            
            if (!locks.TryAdd(DeviceId, DateTime.Now))
            {
                return BadRequest("Last request is in processing.");
            }

            try
            {
                // Read exist file list
                if (AppendMode && System.IO.File.Exists($"{hostEnv.WebRootPath}/userdata/{DeviceId}.txt"))
                {
                    foreach (var fn in System.IO.File.ReadLines($"{hostEnv.WebRootPath}/userdata/{DeviceId}.txt"))
                    {
                        FileSet.Add(fn);
                    }
                }

                foreach (var pic in Pictures)
                {
                    if (FileSet.Contains(pic.FileName))
                    {
                        using (var hasher = SHA1.Create())
                        {
                            var oldContent = await System.IO.File.ReadAllBytesAsync($"{hostEnv.WebRootPath}/userdata/{DeviceId}/{pic.FileName}");
                            var oldHash = hasher.ComputeHash(oldContent);

                            using (Stream newFile = pic.OpenReadStream())
                            {
                                var newHash = hasher.ComputeHash(newFile);
                                if (BitConverter.ToString(oldHash) != BitConverter.ToString(newHash))
                                {
                                    ConflictSet.Add(pic.FileName);
                                }
                            }
                        }
                        continue;
                    }

                    FileStream picFs = CreateAndOpenWrite($"{hostEnv.WebRootPath}/userdata/{DeviceId}/{pic.FileName}");
                    await pic.CopyToAsync(picFs);
                    picFs.Close();

                    FileSet.Add(pic.FileName);
                }

                var fsw = new StreamWriter(CreateAndOpenWrite($"{hostEnv.WebRootPath}/userdata/{DeviceId}.txt"));
                foreach (var fn in FileSet)
                {
                    await fsw.WriteLineAsync(fn);
                }
                fsw.Close();

                if (Directory.Exists($"{hostEnv.WebRootPath}/userdata/{DeviceId}"))
                {
                    var dir = new DirectoryInfo($"{hostEnv.WebRootPath}/userdata/{DeviceId}");
                    var files = dir.GetFiles();
                    foreach (var f in files)
                    {
                        
                        if (!FileSet.Contains(f.Name))
                        {
                            f.Delete();
                        }
                    }
                }
                

                return Page();
            }
            finally
            {
                locks.TryRemove(DeviceId, out _);
            }
        }

        private FileStream CreateAndOpenWrite(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            var di = fileInfo.Directory;
            if (!di.Exists)
                di.Create();
            var fs = System.IO.File.OpenWrite(path);
            fs.SetLength(0);
            fs.Flush();
            return fs;
        }

        private readonly IHostingEnvironment hostEnv;

        public UploadModel(IHostingEnvironment _hostEnv)
        {
            hostEnv = _hostEnv;
        }
    }
}