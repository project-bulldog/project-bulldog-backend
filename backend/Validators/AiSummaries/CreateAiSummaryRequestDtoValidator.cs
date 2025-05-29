using backend.Dtos.AiSummaries;
using FluentValidation;

namespace backend.Validators.AiSummaries;

public class CreateAiSummaryRequestDtoValidator : AbstractValidator<CreateAiSummaryRequestDto>
{
    public CreateAiSummaryRequestDtoValidator()
    {
        RuleFor(x => x.InputText)
            .NotEmpty().WithMessage("Input text is required.")
            .MaximumLength(8000).WithMessage("Input text must be 8000 characters or fewer.");
    }
}
