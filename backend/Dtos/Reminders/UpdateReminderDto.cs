using System.ComponentModel.DataAnnotations;

namespace backend.Dtos.Reminders
{
    public class UpdateReminderDto
    {
        [Required]
        public string Message { get; set; } = string.Empty;

        [Required]
        public DateTime ReminderTime { get; set; }

        public Guid? ActionItemId { get; set; } // Optional to change or unset
    }

}
