using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.StudentMentorSubscriptions.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.StudentMentorSubscriptions.Queries.GetStudentMentorQuota;

public class GetStudentMentorQuotaQueryHandler : IRequestHandler<GetStudentMentorQuotaQuery, Result<StudentMentorQuotaDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetStudentMentorQuotaQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<StudentMentorQuotaDto>> Handle(GetStudentMentorQuotaQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var subscription = await _context.StudentMentorSubscriptions
            .AsNoTracking()
            .Include(s => s.MentorPackage)
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription == null)
        {
            return Result<StudentMentorQuotaDto>.Success(new StudentMentorQuotaDto(
                SubscriptionId: null,
                PackageId: null,
                PackageName: null,
                HasActiveSubscription: false,
                SharesFromMentorLimit: 0,
                SharesFromMentorUsed: 0,
                SharesFromMentorRemaining: 0,
                ValidationRequestLimit: 0,
                ValidationRequestsUsed: 0,
                ValidationRequestsRemaining: 0,
                TaskReviewLimit: 0,
                TaskReviewsUsed: 0,
                TaskReviewsRemaining: 0
            ));
        }

        return Result<StudentMentorQuotaDto>.Success(new StudentMentorQuotaDto(
            SubscriptionId: subscription.SubscriptionId,
            PackageId: subscription.MentorPackageId,
            PackageName: subscription.MentorPackage?.Name,
            HasActiveSubscription: true,
            SharesFromMentorLimit: subscription.SharesFromMentorLimit,
            SharesFromMentorUsed: subscription.SharesFromMentorUsed,
            SharesFromMentorRemaining: CalcRemaining(subscription.SharesFromMentorLimit, subscription.SharesFromMentorUsed),
            ValidationRequestLimit: subscription.ValidationRequestLimit,
            ValidationRequestsUsed: subscription.ValidationRequestsUsed,
            ValidationRequestsRemaining: CalcRemaining(subscription.ValidationRequestLimit, subscription.ValidationRequestsUsed),
            TaskReviewLimit: subscription.TaskReviewLimit,
            TaskReviewsUsed: subscription.TaskReviewsUsed,
            TaskReviewsRemaining: CalcRemaining(subscription.TaskReviewLimit, subscription.TaskReviewsUsed)
        ));
    }

    /// <summary>Returns -1 when unlimited, otherwise max(0, limit - used).</summary>
    private static int CalcRemaining(int limit, int used)
        => limit == -1 ? -1 : Math.Max(0, limit - used);
}
