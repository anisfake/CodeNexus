using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Resources.Queries.GetMyResources
{
    public class GetMyResourcesQueryValidator : AbstractValidator<GetMyResourcesQuery>
    {
        public GetMyResourcesQueryValidator()
        {
            RuleFor(x => x.Type)
                .Must(type => string.IsNullOrEmpty(type) || type.Equals("Link", StringComparison.OrdinalIgnoreCase) || type.Equals("File", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Type must be either 'Link' or 'File' if specified.");
            RuleFor(x => x.SortBy)
                .Must(sortBy => string.IsNullOrEmpty(sortBy) || sortBy.Equals("Title", StringComparison.OrdinalIgnoreCase) || sortBy.Equals("UploadedAt", StringComparison.OrdinalIgnoreCase))
                .WithMessage("SortBy must be either 'Title' or 'UploadedAt' if specified.");
            RuleFor(x => x.PageNumber)
                .GreaterThan(0)
                .WithMessage("PageNumber must be greater than 0.");
            RuleFor(x => x.PageSize)
                .GreaterThan(0)
                .WithMessage("PageSize must be greater than 0.");
        }
    }
}
