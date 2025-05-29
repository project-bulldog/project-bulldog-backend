using backend.Dtos.AiSummaries;
using backend.Extensions;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    // api/ai/summarize
    [HttpPost("summarize")]
    public async Task<ActionResult<AiSummaryResponseDto>> SummarizeText([FromBody] CreateAiSummaryRequestDto request)
    {
        var userId = User.GetUserId();
        var response = await _aiService.SummarizeAsync(request, userId);
        return Ok(response);
    }
}
