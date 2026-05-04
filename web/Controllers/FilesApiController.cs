using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatteryPassWeb.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FilesApiController : ControllerBase
{
    [HttpPost("")]
    public IActionResult Upload()
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new { error = "File upload rewrite in progress" });
    }

    [HttpGet("{fileId}")]
    public IActionResult Download(string fileId)
    {
        return NotFound(new { error = "File not found", fileId });
    }
}
