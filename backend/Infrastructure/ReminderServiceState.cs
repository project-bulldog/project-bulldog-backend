namespace backend.Infrastructure;
public class ReminderServiceState
{
    public DateTime LastRunUtc { get; set; } = DateTime.MinValue;
}
