using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.StudentMentorSubscriptions.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.StudentMentorSubscriptions.Queries.GetStudentMentorQuota;

public record GetStudentMentorQuotaQuery : IRequest<Result<StudentMentorQuotaDto>>;
