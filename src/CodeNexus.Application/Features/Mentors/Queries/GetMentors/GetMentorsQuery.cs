using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Mentors.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Mentors.Queries.GetMentors;

public record GetMentorsQuery(
    int PageNumber = 1,
    int PageSize = 12,
    string? SearchTerm = null,
    SubjectCategory? SubjectCategory = null,
    string? SubjectName = null) : IRequest<Result<PaginationDto<MentorListItemDto>>>;
