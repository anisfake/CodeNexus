using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Mentors.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Mentors.Commands.UpsertMentorReview;

public record UpsertMentorReviewCommand(
    Guid MentorId,
    int Score,
    string? Comment) : IRequest<Result<UpsertMentorReviewResponseDto>>;
