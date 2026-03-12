using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subjects.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Subjects.Commands.UpdateSubject;

public record UpdateSubjectCommand(
    Guid SubjectId,
    string Name,
    string? Description,
    string? Color,
    string? Icon,
    SubjectCategory Category
) : IRequest<Result<SubjectDto>>;
