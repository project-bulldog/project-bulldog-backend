using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IUploadService _uploadService;

    public UploadController(IUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    // POST: api/uploads
    [HttpPost]
    public async Task<IActionResult> UploadFile([FromForm] IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest("No file uploaded.");

        await _uploadService.UploadUserFileAsync(file);
        return Ok();
    }
}

