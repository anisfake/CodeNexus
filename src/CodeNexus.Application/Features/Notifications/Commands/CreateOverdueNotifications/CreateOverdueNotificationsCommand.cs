using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notifications.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Notifications.Commands.CreateOverdueNotifications;

public record CreateOverdueNotificationsCommand(IReadOnlyCollection<Guid>? EligibleUserIds = null) : IRequest<Result<CreateOverdueNotificationsResultDto>>;
