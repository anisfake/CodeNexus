using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Dashboard.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Dashboard.Queries.GetMentorDashboardOverview;

public record GetMentorDashboardOverviewQuery() : IRequest<Result<MentorDashboardOverviewResponse>>;
