using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subjects.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Subjects.Commands.CreateSubject;

public record CreateSubjectCommand(
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    SubjectCategory Category,
    List<CreateSubjectGoalRequest>? Goals
) : IRequest<Result<SubjectDto>>;
