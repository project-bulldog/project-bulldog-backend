namespace backend.Models
{
    public class Reminder
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public string Message { get; set; } = string.Empty;
        public DateTime ReminderTime { get; set; }

        public bool IsSent { get; set; } = false;
        public DateTime? SentAt { get; set; }
        public int SendAttempts { get; set; } = 0;
        public int MaxSendAttempts { get; set; } = 3;


        public Guid? ActionItemId { get; set; }
        public ActionItem? ActionItem { get; set; }
    }
}
