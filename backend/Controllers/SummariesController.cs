using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SummariesController : ControllerBase
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<SummariesController> _logger;

    public SummariesController(BulldogDbContext context, ILogger<SummariesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/summaries
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Summary>>> GetSummaries()
    {
        _logger.LogInformation("Fetching all summaries");
        return await _context.Summaries
            .AsNoTracking()
            .Include(s => s.ActionItems)
            .Include(s => s.User)
            .ToListAsync();
    }

    // GET: api/summaries/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Summary>> GetSummary(int id)
    {
        _logger.LogInformation("Fetching summary with id {Id}", id);
        var summary = await _context.Summaries
            .AsNoTracking()
            .Include(s => s.ActionItems)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (summary == null)
        {
            _logger.LogWarning("Summary with id {Id} not found", id);
            return NotFound();
        }

        return summary;
    }

    // POST: api/summaries
    [HttpPost]
    public async Task<ActionResult<Summary>> CreateSummary(Summary summary)
    {
        _logger.LogInformation("Creating a new summary");
        _context.Summaries.Add(summary);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Summary created with id {Id}", summary.Id);
        return CreatedAtAction(nameof(GetSummary), new { id = summary.Id }, summary);
    }

    // PUT: api/summaries/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSummary(int id, Summary summary)
    {
        if (id != summary.Id)
        {
            _logger.LogWarning("Update failed: id {Id} does not match summary id {SummaryId}", id, summary.Id);
            return BadRequest();
        }

        _context.Entry(summary).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Summary with id {Id} updated successfully", id);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Summaries.Any(s => s.Id == id))
            {
                _logger.LogWarning("Update failed: summary with id {Id} not found", id);
                return NotFound();
            }
            _logger.LogError("Concurrency error occurred while updating summary with id {Id}", id);
            throw;
        }

        return NoContent();
    }

    // DELETE: api/summaries/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSummary(int id)
    {
        _logger.LogInformation("Deleting summary with id {Id}", id);
        var summary = await _context.Summaries.FindAsync(id);
        if (summary == null)
        {
            _logger.LogWarning("Delete failed: summary with id {Id} not found", id);
            return NotFound();
        }

        _context.Summaries.Remove(summary);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Summary with id {Id} deleted successfully", id);
        return NoContent();
    }
}
