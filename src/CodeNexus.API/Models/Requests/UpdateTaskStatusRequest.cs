using CodeNexus.Domain.Enums;

namespace CodeNexus.API.Models.Requests;

public record UpdateTaskStatusRequest(TaskStatus_ Status);
