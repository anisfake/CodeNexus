using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Notifications.Queries.GetUnreadNotificationCount;

public record GetUnreadNotificationCountQuery : IRequest<Result<int>>;
