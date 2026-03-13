using CodeNexus.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class QuizAttempt
    {
        public Guid AttemptId { get; set; }
        public Guid QuizId { get; set; }
        public virtual Quiz Quiz { get; set; } = null!;
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal? Score { get; set; }
        public QuizAttemptStatus Status { get; set; } = QuizAttemptStatus.InProgress;
        public string? Answers { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
