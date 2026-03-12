using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subjects.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Subjects.Queries.GetSubjects;

public record GetSubjectsQuery(SubjectCategory? Category = null) : IRequest<Result<List<SubjectDto>>>;
