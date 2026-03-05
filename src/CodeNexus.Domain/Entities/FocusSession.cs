using CodeNexus.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class FocusSession
    {
        public Guid SessionId { get; set; }
        public Guid TaskId { get; set; }
        public virtual Tasks Task { get; set; } = null!;
        public string? Title { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int DurationInMinutes { get; set; }
        public SessionType SessionType { get; set; } = SessionType.Pomodoro;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? SubmittedCode { get; set; }
        public string? SubmittedSummary { get; set; }
        public string? AIFeedback { get; set; }
        public int? VerificationScore { get; set; }
        public bool IsVerified { get; set; } = false;

        public virtual ICollection<Note> Notes { get; set; } = new List<Note>();
        public virtual DailyCheckins? DailyCheckin { get; set; }
    }
}
