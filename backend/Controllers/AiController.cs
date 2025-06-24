using backend.Dtos.AiSummaries;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly ILogger<AiController> _logger;

    public AiController(IAiService aiService, ILogger<AiController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    // POST: api/ai/summarize
    [HttpPost("summarize")]
    public async Task<ActionResult<AiSummaryResponseDto>> SummarizeText([FromBody] CreateAiSummaryRequestDto request)
    {
        // Get user timezone from request headers if available
        var userTimeZoneId = GetUserTimeZoneFromHeader();

        if (!string.IsNullOrEmpty(userTimeZoneId))
        {
            _logger.LogInformation("👤 Processing request with user timezone: {UserTimezone}", userTimeZoneId);
        }

        var response = await _aiService.SummarizeAsync(request, userTimeZoneId);
        return Ok(response);
    }

    // POST: api/ai/generate-chunked-summary
    [HttpPost("generate-chunked-summary")]
    public async Task<IActionResult> GenerateChunkedSummary([FromBody] AiChunkedSummaryResponseDto request)
    {
        var summary = await _aiService.SummarizeChunkedAsync(request);
        return Ok(summary);
    }

    // POST: api/ai/generate-chunked-summary-with-action-items
    [HttpPost("generate-chunked-summary-with-action-items")]
    public async Task<IActionResult> GenerateChunkedSummaryWithActionItems([FromBody] AiChunkedSummaryResponseDto request)
    {
        var (summary, actionItems) = await _aiService.SummarizeAndExtractActionItemsChunkedAsync(request);
        return Ok(new AiSummaryWithTasksResponseDto(summary, actionItems));
    }

    // POST: api/ai/generate-chunked-summary-with-action-items-and-save
    [HttpPost("generate-chunked-summary-with-action-items-and-save")]
    public async Task<ActionResult<AiSummaryResponseDto>> GenerateAndSaveChunked([FromBody] AiChunkedSummaryResponseDto request)
    {
        var response = await _aiService.SummarizeAndSaveChunkedAsync(request);
        return Ok(response);
    }

    private string? GetUserTimeZoneFromHeader()
    {
        return Request.Headers.TryGetValue("X-User-TimeZone", out var value)
            ? value.FirstOrDefault()
            : null;
    }
}
