using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.AcceptLearningPathShare;

public class AcceptLearningPathShareCommandHandler : IRequestHandler<AcceptLearningPathShareCommand, Result<LearningPathShareDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AcceptLearningPathShareCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathShareDto>> Handle(AcceptLearningPathShareCommand request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LearningPathShareDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == studentId, cancellationToken);

        if (student == null)
        {
            return Result<LearningPathShareDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(student.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathShareDto>.Failure("ACCESS_DENIED", "Only students can accept a learning path share.");
        }

        var share = await _context.LearningPathShares
            .FirstOrDefaultAsync(s => s.ShareId == request.ShareId && s.StudentId == studentId, cancellationToken);

        if (share == null)
        {
            return Result<LearningPathShareDto>.Failure("SHARE_NOT_FOUND", "Learning path share not found.");
        }

        if (share.Status != LearningPathShareStatus.Pending)
        {
            return Result<LearningPathShareDto>.Failure("INVALID_SHARE_STATE", "Only pending shares can be accepted.");
        }

        share.Status = LearningPathShareStatus.Accepted;
        share.RespondedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<LearningPathShareDto>.Success(new LearningPathShareDto(
            share.ShareId,
            share.PathId,
            share.MentorId,
            share.StudentId,
            share.Status,
            share.SentAt,
            share.RespondedAt
        ));
    }
}
