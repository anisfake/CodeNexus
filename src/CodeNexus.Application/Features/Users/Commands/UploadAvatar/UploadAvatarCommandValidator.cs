using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Users.Commands.UploadAvatar
{
    public class UploadAvatarCommandValidator : AbstractValidator<UploadAvatarCommand>
    {
        public UploadAvatarCommandValidator()
        {
            RuleFor(RuleFor => RuleFor.imageStream)
                .NotNull().WithMessage("Image stream cannot be null.")
                .Must(stream => stream.Length > 0).WithMessage("Image stream cannot be empty.");

            RuleFor(RuleFor => RuleFor.fileName)
                .NotEmpty().WithMessage("File name cannot be empty.")
                .MaximumLength(255).WithMessage("File name cannot exceed 255 characters.");

            RuleFor(RuleFor => RuleFor.fileName)
                .Must(fileName =>
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = System.IO.Path.GetExtension(fileName).ToLower();
                    return allowedExtensions.Contains(fileExtension);
                })
                .WithMessage("Invalid file type. Only .jpg, .jpeg, .png, and .gif are allowed.");
        }
    }
}
