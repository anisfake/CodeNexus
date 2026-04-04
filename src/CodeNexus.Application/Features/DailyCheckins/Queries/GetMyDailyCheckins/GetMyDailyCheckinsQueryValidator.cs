using FluentValidation;

namespace CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckins;

public class GetMyDailyCheckinsQueryValidator : AbstractValidator<GetMyDailyCheckinsQuery>
{
    public GetMyDailyCheckinsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithErrorCode("PAGE_NUMBER_INVALID")
            .WithMessage("Page number must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithErrorCode("PAGE_SIZE_INVALID")
            .WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(50)
            .WithErrorCode("PAGE_SIZE_TOO_LARGE")
            .WithMessage("Page size cannot exceed 50");

        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate.Value.Date <= x.ToDate.Value.Date)
            .WithErrorCode("INVALID_DATE_RANGE")
            .WithMessage("FromDate must be less than or equal to ToDate");
    }
}
