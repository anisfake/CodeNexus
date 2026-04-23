using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorPackages.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.MentorPackages.Commands.UpdateMentorPackage;

public class UpdateMentorPackageCommandHandler : IRequestHandler<UpdateMentorPackageCommand, Result<MentorPackageDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateMentorPackageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MentorPackageDto>> Handle(UpdateMentorPackageCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.MentorPackages
            .FirstOrDefaultAsync(x => x.MentorPackageId == request.MentorPackageId, cancellationToken);

        if (entity == null)
            return Result<MentorPackageDto>.Failure("MENTOR_PACKAGE_NOT_FOUND", "Mentor package not found.");

        var normalizedName = request.Name.Trim();
        var duplicateName = await _context.MentorPackages
            .AsNoTracking()
            .AnyAsync(x => x.MentorPackageId != request.MentorPackageId && x.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (duplicateName)
            return Result<MentorPackageDto>.Failure("PACKAGE_NAME_EXISTS", "A mentor package with this name already exists.");

        entity.Name = normalizedName;
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.PriceVnd = request.PriceVnd;
        entity.SharesFromMentorLimit = request.SharesFromMentorLimit;
        entity.ValidationRequestLimit = request.ValidationRequestLimit;
        entity.TaskReviewLimit = request.TaskReviewLimit;
        entity.IsActive = request.IsActive;
        entity.DisplayOrder = request.DisplayOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result<MentorPackageDto>.Success(ToDto(entity));
    }

    private static MentorPackageDto ToDto(MentorPackage entity)
        => new(
            entity.MentorPackageId,
            entity.Name,
            entity.Description,
            entity.PriceVnd,
            entity.SharesFromMentorLimit,
            entity.ValidationRequestLimit,
            entity.TaskReviewLimit,
            entity.IsActive,
            entity.DisplayOrder,
            entity.CreatedAt,
            entity.UpdatedAt);
}
