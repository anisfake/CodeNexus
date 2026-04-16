namespace CodeNexus.Application.Features.LearningPathShares.DTOs;

public enum LearningPathShareUpdateAction
{
    CreateNewFromLatest = 0,
    UpdateCurrentToLatest = 1,
    DisableUpdateNotifications = 2,
    [System.Obsolete("Use DisableUpdateNotifications")]
    KeepCurrent = DisableUpdateNotifications
}
