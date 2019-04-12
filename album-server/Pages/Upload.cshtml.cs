using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
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

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            foreach (var pic in Pictures)
            {
                var file = System.IO.File.OpenWrite($"~/data/{pic.FileName}");
                pic.CopyTo(file);
                file.Close();
            }

            return Page();
        }
    }
}