using FluentValidation;

namespace CodeNexus.Application.Features.AuditLogs.Queries.GetAuditLogs;

public class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0.")
            .WithErrorCode("INVALID_PAGE_NUMBER");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("Page size must be greater than 0.")
            .WithErrorCode("INVALID_PAGE_SIZE")
            .LessThanOrEqualTo(100)
            .WithMessage("Page size must not exceed 100.")
            .WithErrorCode("INVALID_PAGE_SIZE");

        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("ToDate must be greater than or equal to FromDate.")
            .WithErrorCode("INVALID_DATE_RANGE");

        RuleFor(x => x)
            .Must(x =>
            {
                if (!x.FromDate.HasValue || !x.ToDate.HasValue)
                    return true;
                return (x.ToDate.Value - x.FromDate.Value).TotalDays <= 30;
            })
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("Date range must not exceed 30 days.")
            .WithErrorCode("DATE_RANGE_TOO_LARGE");
    }
}
