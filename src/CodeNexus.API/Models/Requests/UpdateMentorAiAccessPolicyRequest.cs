namespace CodeNexus.API.Models.Requests;

public record UpdateMentorAiAccessPolicyRequest(
    int MentorPaidRequestsMonthlyLimit,
    int MentorDowngradeNotifyCooldownHours);

