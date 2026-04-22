using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Mentors.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Mentors.Queries.GetMyMentorReviews;

public record GetMyMentorReviewsQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<MentorReviewListResponseDto>>;
