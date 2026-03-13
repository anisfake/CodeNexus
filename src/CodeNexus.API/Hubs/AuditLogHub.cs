using CodeNexus.Application.Features.AuditLogs.DTOs;
using CodeNexus.Application.Features.AuditLogs.Queries.GetAuditLogs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CodeNexus.API.Hubs;

[Authorize(Roles = "Admin")]
public class AuditLogHub : Hub
{
    private readonly ISender _sender;

    public AuditLogHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task RequestAuditLogs(
        int pageNumber = 1,
        int pageSize = 10,
        string? action = null,
        string? tableName = null,
        Guid? userId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        AuditLogSortBy sortBy = AuditLogSortBy.Timestamp,
        bool sortDescending = true)
    {
        await Clients.Caller.SendAsync("AuditLogsLoading");

        var query = new GetAuditLogsQuery
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Action = action,
            TableName = tableName,
            UserId = userId,
            FromDate = fromDate,
            ToDate = toDate,
            SortBy = sortBy,
            SortDescending = sortDescending
        };

        var result = await _sender.Send(query);

        if (result.IsSuccess)
        {
            await Clients.Caller.SendAsync("ReceiveAuditLogs", result.Value);
        }
        else
        {
            await Clients.Caller.SendAsync("AuditLogsError", new
            {
                result.ErrorCode,
                result.ErrorMessage
            });
        }
    }

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "AuditLogAdmins");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AuditLogAdmins");
        await base.OnDisconnectedAsync(exception);
    }
}
