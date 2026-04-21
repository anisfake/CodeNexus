using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Mentors.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Mentors.Queries.GetMentors;

public record GetMentorsQuery(
    int PageNumber = 1,
    int PageSize = 12,
    string? SearchTerm = null) : IRequest<Result<PaginationDto<MentorListItemDto>>>;
