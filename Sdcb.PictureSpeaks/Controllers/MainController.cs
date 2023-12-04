using Microsoft.AspNetCore.Mvc;

namespace Sdcb.PictureSpeaks.Controllers;

public class MainController : Controller
{
    [Route("")]
    public IActionResult Index()
    {
        return Ok("Hello World");
    }
}
