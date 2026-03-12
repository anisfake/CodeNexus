using CodeNexus.API.Hubs;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AuditLogs.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Services;

public class AuditLogNotifier : IAuditLogNotifier
{
    private readonly IHubContext<AuditLogHub> _hubContext;

    public AuditLogNotifier(IHubContext<AuditLogHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyAsync(AuditLogResponse auditLog, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group("AuditLogAdmins")
            .SendAsync("ReceiveNewAuditLog", auditLog, cancellationToken);
    }
}
