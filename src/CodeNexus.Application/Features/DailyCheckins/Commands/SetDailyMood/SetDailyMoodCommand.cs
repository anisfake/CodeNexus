using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.DailyCheckin.Commands.SetDailyMood;

public record SetDailyMoodCommand(string Mood) : IRequest<Result>;
