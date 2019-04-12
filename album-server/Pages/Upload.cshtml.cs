using System;
using System.Collections.Generic;
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
    }
}