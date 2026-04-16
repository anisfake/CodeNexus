using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TokenPackages.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TokenPackages.Commands.UpdateTokenPackage;

public class UpdateTokenPackageCommandHandler : IRequestHandler<UpdateTokenPackageCommand, Result<TokenPackageDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateTokenPackageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<TokenPackageDto>> Handle(UpdateTokenPackageCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.TokenPackages
            .FirstOrDefaultAsync(x => x.TokenPackageId == request.TokenPackageId, cancellationToken);
        if (entity == null)
        {
            return Result<TokenPackageDto>.Failure("TOKEN_PACKAGE_NOT_FOUND", "Token package not found.");
        }

        var normalizedName = request.Name.Trim();
        var duplicateName = await _context.TokenPackages
            .AsNoTracking()
            .AnyAsync(x => x.TokenPackageId != request.TokenPackageId && x.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (duplicateName)
        {
            return Result<TokenPackageDto>.Failure("TOKEN_PACKAGE_EXISTS", "Token package name already exists.");
        }

        entity.Name = normalizedName;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.PriceVnd = request.PriceVnd;
        entity.CreditedTokens = request.CreditedTokens;
        entity.IsActive = request.IsActive;
        entity.DisplayOrder = request.DisplayOrder;
        entity.UpdatedAt = DateTime.UtcNow;

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

