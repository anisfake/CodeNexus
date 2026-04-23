using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorPackages.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.MentorPackages.Commands.CreateMentorPackage;

public class CreateMentorPackageCommandHandler : IRequestHandler<CreateMentorPackageCommand, Result<MentorPackageDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateMentorPackageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MentorPackageDto>> Handle(CreateMentorPackageCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        var existed = await _context.MentorPackages
            .AsNoTracking()
            .AnyAsync(x => x.Name.ToLower() == normalizedName.ToLower(), cancellationToken);

        if (existed)
            return Result<MentorPackageDto>.Failure("PACKAGE_NAME_EXISTS", "A mentor package with this name already exists.");

        var entity = new MentorPackage
        {
            MentorPackageId = NewId.NextGuid(),
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PriceVnd = request.PriceVnd,
            SharesFromMentorLimit = request.SharesFromMentorLimit,
            ValidationRequestLimit = request.ValidationRequestLimit,
            TaskReviewLimit = request.TaskReviewLimit,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = DateTime.UtcNow
        };

        await _context.MentorPackages.AddAsync(entity, cancellationToken);
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
