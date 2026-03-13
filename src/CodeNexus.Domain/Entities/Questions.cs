using CodeNexus.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class Questions
    {
        public Guid QuestionId { get; set; }
        public Guid QuizId { get; set; }
        public virtual Quiz Quiz { get; set; } = null!;
        public string QuestionText { get; set; } = string.Empty;
        public QuestionType? Type { get; set; }
        public string? Options { get; set; }
        public string? CorrectAnswer { get; set; }
        public decimal Points { get; set; } = 1;
        public int? OrderIndex { get; set; }
    }
}
