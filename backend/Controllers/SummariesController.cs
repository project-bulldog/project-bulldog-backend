using backend.Dtos.Summaries;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SummariesController : ControllerBase
{
    private readonly ISummaryService _summaryService;
    private readonly IChunkedSummarizerService _chunkedSummarizerService;
    private readonly ILogger<SummariesController> _logger;

    public SummariesController(ISummaryService summaryService, IChunkedSummarizerService chunkedSummarizerService, ILogger<SummariesController> logger)
    {
        _summaryService = summaryService;
        _chunkedSummarizerService = chunkedSummarizerService;
        _logger = logger;
    }

    // GET: api/summaries
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SummaryDto>>> GetSummaries()
    {
        _logger.LogInformation("Fetching all summaries");
        var summaries = await _summaryService.GetSummariesAsync();
        return Ok(summaries);
    }

    // GET: api/summaries/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<SummaryDto>> GetSummary(Guid id)
    {
        _logger.LogInformation("Fetching summary with id {Id}", id);
        var summary = await _summaryService.GetSummaryAsync(id);

        if (summary == null)
        {
            _logger.LogWarning("Summary with id {Id} not found", id);
            return NotFound();
        }

        return Ok(summary);
    }

    // POST: api/summaries
    [HttpPost]
    public async Task<ActionResult<CreateSummaryDto>> CreateSummary(CreateSummaryDto summary)
    {
        _logger.LogInformation("Creating a new summary");
        var createdSummary = await _summaryService.CreateSummaryAsync(summary);

        _logger.LogInformation("Summary created with id {Id}", createdSummary.Id);
        return CreatedAtAction(nameof(GetSummary), new { id = createdSummary.Id }, createdSummary);
    }

    // POST: api/summaries/chunked-summary
    [HttpPost("chunked-summary")]
    public async Task<IActionResult> GetChunkedSummary([FromBody] string input)
    {
        var model = Request.Headers["X-Model"].FirstOrDefault() ?? "gpt-3.5-turbo";
        var useMapReduce = Request.Headers["X-MapReduce"].FirstOrDefault()?.ToLower() != "false";

        var summary = await _chunkedSummarizerService.SummarizeLongTextAsync(input, useMapReduce, model);
        return Ok(summary);
    }

    // PUT: api/summaries/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSummary(Guid id, UpdateSummaryDto updateDto)
    {
        var updateResult = await _summaryService.UpdateSummaryAsync(id, updateDto);

        if (!updateResult)
        {
            _logger.LogWarning("Update failed: summary with id {Id} not found", id);
            return NotFound();
        }

        _logger.LogInformation("Summary with id {Id} updated successfully", id);
        return NoContent();
    }

    // DELETE: api/summaries/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSummary(Guid id)
    {
        _logger.LogInformation("Deleting summary with id {Id}", id);
        var deleteResult = await _summaryService.DeleteSummaryAsync(id);

        if (!deleteResult)
        {
            _logger.LogWarning("Delete failed: summary with id {Id} not found", id);
            return NotFound();
        }

        _logger.LogInformation("Summary with id {Id} deleted successfully", id);
        return NoContent();
    }
}
