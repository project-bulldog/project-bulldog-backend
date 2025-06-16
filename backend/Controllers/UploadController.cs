using backend.Dtos.AiSummaries;
using backend.Services.FileUpload.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
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

        // POST /api/uploads
        [HttpPost]
        public async Task<ActionResult<AiSummaryWithTasksResponseDto>> Post([FromForm] IFormFile file)
        {
            if (file is null)
                return BadRequest("No file provided.");

            // This will:
            //  1) store the blob
            //  2) call the Azure Function
            //  3) get back { summary, tasks }
            var result = await _uploadService.UploadUserFileAsync(file);

            return Ok(result);
        }
    }
}