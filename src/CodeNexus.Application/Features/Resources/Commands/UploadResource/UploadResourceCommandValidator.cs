using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Resources.Commands.UploadResource
{
    public class UploadResourceCommandValidator : AbstractValidator<UploadResourceCommand>
    {
        public UploadResourceCommandValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(100).WithMessage("Title must not exceed 100 characters.");
            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("Invalid resource type.");
            RuleFor(x => x.Url)
                .MaximumLength(500).WithMessage("URL must not exceed 500 characters.")
                .When(x => x.Type == Domain.Enums.ResourceType.Link && !string.IsNullOrEmpty(x.Url));
            RuleFor(x => x.Description)
                .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters.")
                .When(x => !string.IsNullOrEmpty(x.Description));
            RuleFor(x => x.SubjectId)
                .NotEmpty().WithMessage("SubjectId is required.");
        }
    }
}
