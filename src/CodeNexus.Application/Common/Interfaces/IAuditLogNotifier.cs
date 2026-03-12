using CodeNexus.Application.Features.AuditLogs.DTOs;

namespace CodeNexus.Application.Common.Interfaces;

public interface IAuditLogNotifier
{
    Task NotifyAsync(AuditLogResponse auditLog, CancellationToken cancellationToken = default);
}
