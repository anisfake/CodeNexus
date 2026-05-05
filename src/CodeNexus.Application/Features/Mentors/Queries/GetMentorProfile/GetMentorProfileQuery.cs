using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Mentors.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Mentors.Queries.GetMentorProfile;

public record GetMentorProfileQuery(Guid MentorId) : IRequest<Result<MentorProfileDto>>;
