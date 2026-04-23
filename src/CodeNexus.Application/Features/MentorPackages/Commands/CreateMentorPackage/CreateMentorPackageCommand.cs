using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorPackages.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.MentorPackages.Commands.CreateMentorPackage;

public record CreateMentorPackageCommand(
    string Name,
    string? Description,
    decimal PriceVnd,
    int SharesFromMentorLimit,
    int ValidationRequestLimit,
    int TaskReviewLimit,
    bool IsActive,
    int DisplayOrder
) : IRequest<Result<MentorPackageDto>>;
