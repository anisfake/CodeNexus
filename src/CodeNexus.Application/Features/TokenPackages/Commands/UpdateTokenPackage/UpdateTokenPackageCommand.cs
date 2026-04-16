using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TokenPackages.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.TokenPackages.Commands.UpdateTokenPackage;

public record UpdateTokenPackageCommand(
    Guid TokenPackageId,
    string Name,
    string? Description,
    decimal PriceVnd,
    decimal CreditedTokens,
    bool IsActive,
    int DisplayOrder
) : IRequest<Result<TokenPackageDto>>;

