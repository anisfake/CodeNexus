using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.MentorPackages.Commands.DeleteMentorPackage;

public record DeleteMentorPackageCommand(Guid MentorPackageId) : IRequest<Result<string>>;
