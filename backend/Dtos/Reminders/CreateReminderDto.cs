using System.ComponentModel.DataAnnotations;

namespace backend.Dtos.Reminders
{
    public class CreateReminderDto
    {
        [Required]
        public string Message { get; set; } = string.Empty;

        [Required]
        public DateTime ReminderTime { get; set; }

        public Guid? ActionItemId { get; set; } // Optional association
    }
}
