using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AIUsageLogs.Queries.GetMentorAiQuotaStatus;

public class GetMentorAiQuotaStatusQueryHandler
    : IRequestHandler<GetMentorAiQuotaStatusQuery, Result<PaginationDto<MentorAiQuotaStatusResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAIAccessPolicyService _aiAccessPolicyService;

    public GetMentorAiQuotaStatusQueryHandler(
        IApplicationDbContext context,
        IAIAccessPolicyService aiAccessPolicyService)
    {
        _context = context;
        _aiAccessPolicyService = aiAccessPolicyService;
    }

    public async Task<Result<PaginationDto<MentorAiQuotaStatusResponse>>> Handle(
        GetMentorAiQuotaStatusQuery request,
        CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;
        var nearThresholdPercent = Math.Clamp(request.NearThresholdPercent, 1, 100);
        var nearThresholdRatio = nearThresholdPercent / 100m;

        var mentorRoleIds = await _context.Roles
            .AsNoTracking()
            .Where(r => r.RoleName == "Mentor")
            .Select(r => r.RoleId)
            .ToListAsync(cancellationToken);

        if (mentorRoleIds.Count == 0)
        {
            return Result<PaginationDto<MentorAiQuotaStatusResponse>>.Success(new PaginationDto<MentorAiQuotaStatusResponse>
            {
                Items = new List<MentorAiQuotaStatusResponse>(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = 0
            });
        }

        var mentorsQuery = _context.Users
            .AsNoTracking()
            .Where(u => u.RoleId.HasValue && mentorRoleIds.Contains(u.RoleId.Value));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            mentorsQuery = mentorsQuery.Where(u =>
                u.Username.ToLower().Contains(search) ||
                u.Email.ToLower().Contains(search));
        }

        var mentors = await mentorsQuery
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.Email
            })
            .ToListAsync(cancellationToken);

        var monthlyLimit = await _aiAccessPolicyService.GetMentorPaidRequestsMonthlyLimitAsync(cancellationToken);
        var windowStartUtc = GetCurrentMonthStartUtc();
        var mentorIds = mentors.Select(m => m.UserId).ToList();

        var usageByMentor = await _context.FeatureUsageLogs
            .AsNoTracking()
            .Where(x =>
                x.FeatureKey == SubscriptionFeatureKey.MentorPaidAiRequests
                && x.CreatedAt >= windowStartUtc
                && mentorIds.Contains(x.UserId))
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Used = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Used, cancellationToken);

        var data = mentors.Select(m =>
        {
            var used = usageByMentor.TryGetValue(m.UserId, out var count) ? count : 0;
            var ratio = monthlyLimit <= 0 ? 1m : Math.Round((decimal)used / monthlyLimit, 4);
            var isReached = monthlyLimit <= 0 || used >= monthlyLimit;
            var isNear = !isReached && ratio >= nearThresholdRatio;

            return new MentorAiQuotaStatusResponse(
                m.UserId,
                m.Username,
                m.Email,
                used,
                monthlyLimit,
                ratio,
                isNear,
                isReached,
                windowStartUtc);
        });

        if (request.OnlyNearOrReached)
        {
            data = data.Where(x => x.IsNearLimit || x.IsReachedLimit);
        }

        var ordered = data
            .OrderByDescending(x => x.IsReachedLimit)
            .ThenByDescending(x => x.UsageRatio)
            .ThenByDescending(x => x.UsedPaidRequestsThisMonth)
            .ThenBy(x => x.Username)
            .ToList();

        var totalCount = ordered.Count;
        var items = ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result<PaginationDto<MentorAiQuotaStatusResponse>>.Success(new PaginationDto<MentorAiQuotaStatusResponse>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    private static DateTime GetCurrentMonthStartUtc()
    {
        var timezone = ResolveVietnamTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
        var startLocal = new DateTime(nowLocal.Year, nowLocal.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(startLocal, timezone);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }
}
