using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Subjects.Commands.DeleteSubject;

public record DeleteSubjectCommand(Guid SubjectId) : IRequest<Result<string>>;
