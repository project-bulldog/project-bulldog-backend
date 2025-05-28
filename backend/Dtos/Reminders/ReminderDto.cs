namespace backend.Dtos.Reminders
{
    public class ReminderDto
    {
        public Guid Id { get; set; }

        public string Message { get; set; } = string.Empty;

        public DateTime ReminderTime { get; set; }

        public bool IsSent { get; set; }

        public Guid? ActionItemId { get; set; }
    }
}
