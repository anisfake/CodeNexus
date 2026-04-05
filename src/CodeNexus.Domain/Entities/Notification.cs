using CodeNexus.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class Notification
    {
        public Guid NotificationId { get; set; }
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public NotificationType? Type { get; set; }
        public string Severity { get; set; } = "Info";
        public string Channels { get; set; } = "Web";
        public string? TargetType { get; set; }
        public Guid? TargetId { get; set; }
        public string? TargetUrl { get; set; }
        public string? Route { get; set; }
        public Guid? TaskId { get; set; }
        public Guid? ChapterId { get; set; }
        public Guid? LessonId { get; set; }
        public Guid? LearningPathId { get; set; }
        public string? NotifiedPathTitle { get; set; }
        public int? NotifiedSourceVersion { get; set; }
        public string? NotifiedMentorUserName { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
    }
}
