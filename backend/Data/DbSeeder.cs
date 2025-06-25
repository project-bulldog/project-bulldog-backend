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
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };

        // Create summaries for the user
        var summary1 = new Summary
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OriginalText = "Meeting about Q2 goals and upcoming launch.",
            SummaryText = "Discussed Q2 goals and set milestones for product launch.",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAtLocal = DateTime.UtcNow, // Test data uses UTC
            User = user
        };

        var summary2 = new Summary
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OriginalText = "Follow-up on design sprint and feedback.",
            SummaryText = "Reviewed sprint feedback and outlined next steps.",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAtLocal = DateTime.UtcNow, // Test data uses UTC
            User = user
        };

        // Create action items for summaries
        var actionItem1 = new ActionItem
        {
            Id = Guid.NewGuid(),
            Text = "Email project timeline to stakeholders.",
            IsDone = false,
            DueAt = DateTime.UtcNow.AddDays(2),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAtLocal = DateTime.UtcNow, // Test data uses UTC
            Summary = summary1
        };

        var actionItem2 = new ActionItem
        {
            Id = Guid.NewGuid(),
            Text = "Prepare slides for Q2 presentation.",
            IsDone = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAtLocal = DateTime.UtcNow, // Test data uses UTC
            Summary = summary1
        };

        var actionItem3 = new ActionItem
        {
            Id = Guid.NewGuid(),
            Text = "Schedule next design sprint meeting.",
            IsDone = true,
            DueAt = DateTime.UtcNow.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAtLocal = DateTime.UtcNow, // Test data uses UTC
            Summary = summary2
        };

        // Save users, summaries, action items
        await context.Users.AddAsync(user);
        await context.Summaries.AddRangeAsync(summary1, summary2);
        await context.ActionItems.AddRangeAsync(actionItem1, actionItem2, actionItem3);
        await context.SaveChangesAsync(); // ✅ Save before adding reminders

        // Now create reminders (linked + standalone)
        var reminders = new List<Reminder>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Message = "Follow up on Q2 timeline email.",
                ReminderTime = DateTime.UtcNow.AddHours(12),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedAtLocal = DateTime.UtcNow, // Test data uses UTC
                ActionItem = actionItem1
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Message = "Start working on slides tomorrow.",
                ReminderTime = DateTime.UtcNow.AddDays(1),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedAtLocal = DateTime.UtcNow, // Test data uses UTC
                ActionItem = actionItem2
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Message = "Check in on design sprint meeting outcome.",
                ReminderTime = DateTime.UtcNow.AddDays(3),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedAtLocal = DateTime.UtcNow // Test data uses UTC
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Message = "Pay invoices by the 15th.",
                ReminderTime = DateTime.UtcNow.AddDays(5),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedAtLocal = DateTime.UtcNow // Test data uses UTC
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Message = "Review monthly budget.",
                ReminderTime = DateTime.UtcNow.AddDays(7),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedAtLocal = DateTime.UtcNow // Test data uses UTC
            }
        };

        await context.Reminders.AddRangeAsync(reminders);
        await context.SaveChangesAsync();
    }
}
