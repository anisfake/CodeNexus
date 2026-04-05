using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notifications.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public record MarkNotificationAsReadCommand(IReadOnlyCollection<Guid> NotificationIds) : IRequest<Result<MarkNotificationAsReadResultDto>>;
