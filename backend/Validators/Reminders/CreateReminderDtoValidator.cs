using backend.Dtos.Reminders;
using FluentValidation;

namespace backend.Validators.Reminders;

public class CreateReminderDtoValidator : AbstractValidator<CreateReminderDto>
{
    public CreateReminderDtoValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Reminder message is required.")
            .MaximumLength(500).WithMessage("Message must be 500 characters or fewer.");

        RuleFor(x => x.ReminderTime)
            .Must(BeInTheFuture).WithMessage("Reminder time must be in the future.");

        // If ActionItemId is present, must be a valid Guid
        RuleFor(x => x.ActionItemId)
            .Must(id => id == null || id != Guid.Empty)
            .WithMessage("Invalid ActionItem ID.");
    }

    private bool BeInTheFuture(DateTime reminderTime)
    {
        return reminderTime > DateTime.UtcNow;
    }
}
