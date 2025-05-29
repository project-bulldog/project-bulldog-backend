using backend.Dtos.Reminders;
using FluentValidation;

namespace backend.Validators.Reminders;

public class UpdateReminderDtoValidator : AbstractValidator<UpdateReminderDto>
{
    public UpdateReminderDtoValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Reminder message is required.")
            .MaximumLength(500).WithMessage("Message must be 500 characters or fewer.");

        RuleFor(x => x.ReminderTime)
            .Must(BeInTheFuture).WithMessage("Reminder time must be in the future.");
    }

    private bool BeInTheFuture(DateTime reminderTime)
    {
        return reminderTime > DateTime.UtcNow;
    }
}
