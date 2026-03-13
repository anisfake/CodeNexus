using FluentValidation;

namespace CodeNexus.Application.Features.FocusSessions.Queries.GetActiveSession;

public class GetActiveSessionQueryValidator : AbstractValidator<GetActiveSessionQuery>
{
    public GetActiveSessionQueryValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty()
            .WithMessage("TaskId is required");
    }
}