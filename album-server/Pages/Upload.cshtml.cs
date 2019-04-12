using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
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

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            foreach (var pic in Pictures)
            {
                var path = $"{hostEnv.WebRootPath}/userdata/{DeviceId}/{pic.FileName}";
                FileInfo fileInfo = new FileInfo(path);
                var di = fileInfo.Directory;
                if (!di.Exists)
                    di.Create();
                FileStream file = System.IO.File.OpenWrite(path);
                pic.CopyTo(file);
                file.Close();
            }

            return Page();
        }

        private readonly IHostingEnvironment hostEnv;

        public UploadModel(IHostingEnvironment _hostEnv)
        {
            hostEnv = _hostEnv;
        }
    }
}