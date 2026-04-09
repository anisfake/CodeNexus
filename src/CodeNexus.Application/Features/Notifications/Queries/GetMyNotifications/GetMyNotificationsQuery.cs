using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notifications.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Notifications.Queries.GetMyNotifications;

public record GetMyNotificationsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    bool UnreadOnly = false,
    NotificationType? Type = null)
    : IRequest<Result<NotificationPagedResultDto>>;
