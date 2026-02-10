using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Dashboard.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Dashboard.Queries.GetStudentDashboardStats;

public record GetStudentDashboardStatsQuery() : IRequest<Result<StudentDashboardStatsResponse>>;
