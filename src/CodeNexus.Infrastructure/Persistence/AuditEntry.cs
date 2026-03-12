using CodeNexus.Domain.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace CodeNexus.Infrastructure.Persistence;

internal class AuditEntry
{
    public EntityEntry Entry { get; set; } = null!;
    public Guid? UserId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Guid? RecordId { get; set; }
    public Dictionary<string, object?> OldValues { get; } = new();
    public Dictionary<string, object?> NewValues { get; } = new();
    public List<PropertyEntry> TemporaryProperties { get; } = new();
    public string? IPAddress { get; set; }

    public bool HasTemporaryProperties => TemporaryProperties.Count > 0;

    public AuditLog ToAuditLog()
    {
        return new AuditLog
        {
            UserId = UserId,
            Action = Action,
            TableName = TableName,
            RecordId = RecordId,
            OldValue = OldValues.Count > 0 ? JsonSerializer.Serialize(OldValues) : null,
            NewValue = NewValues.Count > 0 ? JsonSerializer.Serialize(NewValues) : null,
            Timestamp = DateTime.UtcNow,
            IPAddress = IPAddress
        };
    }
}
