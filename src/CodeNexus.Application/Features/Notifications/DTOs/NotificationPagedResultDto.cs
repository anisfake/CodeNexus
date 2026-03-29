namespace CodeNexus.Application.Features.Notifications.DTOs;

public record NotificationPagedResultDto(
    IReadOnlyList<NotificationDto> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    bool HasNextPage,
    bool HasPreviousPage
);
