using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TokenPackages.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TokenPackages.Commands.CreateTokenPackage;

public class CreateTokenPackageCommandHandler : IRequestHandler<CreateTokenPackageCommand, Result<TokenPackageDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateTokenPackageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TokenPackageDto>> Handle(CreateTokenPackageCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        var existed = await _context.TokenPackages
            .AsNoTracking()
            .AnyAsync(x => x.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (existed)
        {
            return Result<TokenPackageDto>.Failure("TOKEN_PACKAGE_EXISTS", "Token package name already exists.");
        }

        var entity = new TokenPackage
        {
            TokenPackageId = NewId.NextGuid(),
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PriceVnd = request.PriceVnd,
            CreditedTokens = request.CreditedTokens,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTime.UtcNow
        };

        await _context.TokenPackages.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<TokenPackageDto>.Success(ToDto(entity));
    }

    private static TokenPackageDto ToDto(TokenPackage entity)
        => new(
            entity.TokenPackageId,
            entity.Name,
            entity.Description,
            entity.PriceVnd,
            entity.CreditedTokens,
            entity.IsActive,
            entity.DisplayOrder,
            entity.CreatedAt,
            entity.UpdatedAt);
}

