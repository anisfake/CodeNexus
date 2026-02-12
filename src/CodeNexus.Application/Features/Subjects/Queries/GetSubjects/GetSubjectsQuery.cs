using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subjects.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Subjects.Queries.GetSubjects;

public record GetSubjectsQuery : IRequest<Result<List<SubjectDto>>>;
