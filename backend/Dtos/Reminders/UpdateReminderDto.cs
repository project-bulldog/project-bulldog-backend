using System.ComponentModel.DataAnnotations;

namespace backend.Dtos.Reminders
{
    public class UpdateReminderDto
    {
        public string Message { get; set; } = string.Empty;

        public DateTime ReminderTime { get; set; }

        public Guid ActionItemId { get; set; }
    }
}
