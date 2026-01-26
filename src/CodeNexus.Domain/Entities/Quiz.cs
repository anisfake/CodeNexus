using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class Quiz
    {
        public Guid QuizId { get; set; }
        public Guid? LessonId { get; set; }
        public virtual Lesson? Lesson { get; set; }

        public Guid? SummaryId { get; set; }
        public virtual AISummary? Summary { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? TimeLimit { get; set; }
        public decimal? PassingScore { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<Questions> Questions { get; set; } = new List<Questions>();
        public virtual ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();
    }
}
