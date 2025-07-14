public class ReminderDto
{
    public Guid Id { get; set; }

    public string Message { get; set; } = string.Empty;

    public DateTime ReminderTime { get; set; }

    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }

    public Guid? ActionItemId { get; set; }

    public bool IsActive { get; set; }
    public bool IsMissed { get; set; }
    public DateTime? SnoozedUntil { get; set; }
}
