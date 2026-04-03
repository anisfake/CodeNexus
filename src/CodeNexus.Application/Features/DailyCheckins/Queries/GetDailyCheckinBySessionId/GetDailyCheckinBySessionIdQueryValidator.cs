using FluentValidation;

namespace CodeNexus.Application.Features.DailyCheckin.Queries.GetDailyCheckinBySessionId;

public class GetDailyCheckinBySessionIdQueryValidator : AbstractValidator<GetDailyCheckinBySessionIdQuery>
{
    public GetDailyCheckinBySessionIdQueryValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithErrorCode("SESSION_ID_REQUIRED")
            .WithMessage("SessionId is required");
    }
}
