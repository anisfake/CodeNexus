namespace CodeNexus.Application.Features.AuditLogs.DTOs;

public record AuditLogResponse(
    Guid LogId,
    Guid? UserId,
    string? Username,
    string Action,
    string? TableName,
    Guid? RecordId,
    string? OldValue,
    string? NewValue,
    DateTime Timestamp,
    string? IPAddress
);
