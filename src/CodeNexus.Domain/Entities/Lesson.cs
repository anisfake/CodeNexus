using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class Lesson
    {
        public Guid LessonId { get; set; }
        public Guid ChapterId { get; set; }
        public virtual Chapter Chapter { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    }
}
