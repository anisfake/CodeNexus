namespace CodeNexus.Application.Features.AIAccessPolicy.DTOs;

public record MentorAiAccessPolicyDto(
    int MentorPaidRequestsMonthlyLimit,
    int MentorDowngradeNotifyCooldownHours,
    DateTime UpdatedAt);

