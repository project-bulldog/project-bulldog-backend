using backend.Dtos.Reminders;
using FluentValidation;

namespace backend.Validators.Reminders;

public class SnoozeReminderDtoValidator : AbstractValidator<SnoozeReminderDto>
{
    public SnoozeReminderDtoValidator()
    {
        RuleFor(x => x.SnoozeMinutes)
            .GreaterThan(0).WithMessage("Snooze time must be greater than 0 minutes.")
            .LessThanOrEqualTo(1440).WithMessage("Snooze time cannot exceed 24 hours.");
    }
}
