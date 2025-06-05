using backend.Dtos.Users;
using FluentValidation;

namespace backend.Validators.Users;

public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserDtoValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .EmailAddress().WithMessage("Invalid email format.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.DisplayName)
            .MaximumLength(50).WithMessage("Display name must be 50 characters or fewer.")
            .When(x => !string.IsNullOrWhiteSpace(x.DisplayName));
    }
}
