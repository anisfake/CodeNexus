using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.ChannelMessages.DTOs;

public record ChannelDto(
    SubjectCategory Category,
    string Name
);
