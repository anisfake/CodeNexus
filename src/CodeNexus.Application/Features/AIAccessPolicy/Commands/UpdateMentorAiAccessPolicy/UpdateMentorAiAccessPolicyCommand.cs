using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIAccessPolicy.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.AIAccessPolicy.Commands.UpdateMentorAiAccessPolicy;

public record UpdateMentorAiAccessPolicyCommand(
    int MentorPaidRequestsMonthlyLimit,
    int MentorDowngradeNotifyCooldownHours) : IRequest<Result<MentorAiAccessPolicyDto>>;

