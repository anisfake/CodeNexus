namespace CodeNexus.Domain.Enums
{
    public enum SessionStatus
    {
        Running = 0,
        Paused = 1,
        CompletedEarly = 2,
        CompletedOnTime = 3,
        CompletedLate = 4,
        Abandoned = 5
    }
}
