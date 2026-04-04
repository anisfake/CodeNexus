using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIAccessPolicy.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.AIAccessPolicy.Queries.GetMentorAiAccessPolicy;

public record GetMentorAiAccessPolicyQuery : IRequest<Result<MentorAiAccessPolicyDto>>;

