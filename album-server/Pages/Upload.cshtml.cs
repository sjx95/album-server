using System;
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

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

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
                    var oldFile = System.IO.File.OpenRead($"{hostEnv.WebRootPath}/userdata/{DeviceId}/{pic.FileName}");

                    bool conflict = false;
                    if (pic.Length == oldFile.Length)
                    {
                        var newFile = pic.OpenReadStream();
                        for (long i = 0; i < pic.Length; ++i)
                        {
                            if (newFile.ReadByte() != oldFile.ReadByte())
                            {
                                conflict = true;
                                break;
                            }
                        }
                        newFile.Close();
                    }
                    else
                    {
                        conflict = true;
                    }

                    oldFile.Close();

                    if (conflict)
                    {
                        ConflictSet.Add(pic.FileName);
                    }

                    continue;
                }

                FileStream picFs = CreateAndOpenWrite($"{hostEnv.WebRootPath}/userdata/{DeviceId}/{pic.FileName}");
                pic.CopyTo(picFs);
                picFs.Close();

                FileSet.Add(pic.FileName);
            }

            var fsw = new StreamWriter(CreateAndOpenWrite($"{hostEnv.WebRootPath}/userdata/{DeviceId}.txt"));
            foreach (var fn in FileSet)
            {
                fsw.WriteLine(fn);
            }
            fsw.Close();

            return Page();
        }

        private FileStream CreateAndOpenWrite(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            var di = fileInfo.Directory;
            if (!di.Exists)
                di.Create();
            return System.IO.File.OpenWrite(path);
        }

        private readonly IHostingEnvironment hostEnv;

        public UploadModel(IHostingEnvironment _hostEnv)
        {
            hostEnv = _hostEnv;
        }
    }
}