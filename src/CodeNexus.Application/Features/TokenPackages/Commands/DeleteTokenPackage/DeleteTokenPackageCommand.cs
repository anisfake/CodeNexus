using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.TokenPackages.Commands.DeleteTokenPackage;

public record DeleteTokenPackageCommand(Guid TokenPackageId) : IRequest<Result<string>>;
