using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notifications.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Notifications.Queries.GetMyNotifications;

public record GetMyNotificationsQuery(int PageNumber = 1, int PageSize = 20, bool UnreadOnly = false)
    : IRequest<Result<List<NotificationItemDto>>>;
