using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Timeline.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Timeline.Queries.GetStudentTimeline;

public record GetStudentTimelineQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Guid? LearningPathId = null,
    bool OnlyActivePaths = true) : IRequest<Result<StudentTimelineResponse>>;

