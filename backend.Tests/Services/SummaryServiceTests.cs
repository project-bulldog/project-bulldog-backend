using backend.Data;
using backend.Dtos;
using backend.Models;
using backend.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests.Services;

public class SummaryServiceTests
{
    private BulldogDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new BulldogDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task CreateAsync_ShouldAddNewSummary()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var service = new SummaryService(context);

        var dto = new CreateSummaryDto
        {
            UserId = Guid.NewGuid(),
            OriginalText = "Original",
            SummaryText = "Summary"
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        var saved = await context.Summaries.FirstOrDefaultAsync(s => s.Id == result.Id);
        Assert.NotNull(saved);
        Assert.Equal(dto.OriginalText, saved.OriginalText);
        Assert.Equal(dto.SummaryText, saved.SummaryText);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCorrectSummary()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var summary = new Summary
        {
            UserId = Guid.NewGuid(),
            OriginalText = "Original",
            SummaryText = "Summary",
            CreatedAt = DateTime.UtcNow
        };
        context.Summaries.Add(summary);
        await context.SaveChangesAsync();

        var service = new SummaryService(context);

        // Act
        var result = await service.GetByIdAsync(summary.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(summary.OriginalText, result!.OriginalText);
    }
}
