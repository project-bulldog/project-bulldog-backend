using backend.Models;

namespace backend.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(BulldogDbContext context)
    {
        if (context.Users.Any()) return; // prevent reseeding

        // Create a user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "testuser@example.com",
            DisplayName = "Test User"
        };

        // Create summaries for the user
        var summary1 = new Summary
        {
            UserId = user.Id,
            OriginalText = "Meeting about Q2 goals and upcoming launch.",
            SummaryText = "Discussed Q2 goals and set milestones for product launch.",
            CreatedAt = DateTime.UtcNow,
            User = user
        };

        var summary2 = new Summary
        {
            UserId = user.Id,
            OriginalText = "Follow-up on design sprint and feedback.",
            SummaryText = "Reviewed sprint feedback and outlined next steps.",
            CreatedAt = DateTime.UtcNow,
            User = user
        };

        // Create action items for summaries
        var actionItems = new List<ActionItem>
        {
            new() {
                Text = "Email project timeline to stakeholders.",
                IsDone = false,
                DueAt = DateTime.UtcNow.AddDays(2),
                Summary = summary1
                },
            new() {
                Text = "Prepare slides for Q2 presentation.",
                IsDone = false,
                Summary = summary1
                },
            new() {
                Text = "Schedule next design sprint meeting.",
                IsDone = true,
                DueAt = DateTime.UtcNow.AddDays(1),
                Summary = summary2
                }
        };
        // Add everything to context
        await context.Users.AddAsync(user);
        await context.Summaries.AddRangeAsync(summary1, summary2);
        await context.ActionItems.AddRangeAsync(actionItems);

        await context.SaveChangesAsync();
    }
}
