using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Domain.Entities
{
    public class User
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }
        public string? Status { get; set; } = "Active";
        public Guid? RoleId { get; set; }
        public virtual Role? Role { get; set; }
        public virtual UserProfile? UserProfile { get; set; }
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public virtual ICollection<Subject> Subjects { get; set; } = new List<Subject>(); // Subjects created by Mentor
        public virtual ICollection<LearningPath> LearningPaths { get; set; } = new List<LearningPath>(); // Learning paths created by Student
        public virtual ICollection<Resource> Resources { get; set; } = new List<Resource>(); // Resources uploaded by User
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public virtual ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
        public virtual ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();
        public virtual ICollection<LearnProgress> LearnProgresses { get; set; } = new List<LearnProgress>();
        public virtual ICollection<Goals> Goals { get; set; } = new List<Goals>();
        public Guid? SubscriptionPlanId { get; set; }
        public virtual SubscriptionPlan? SubscriptionPlan { get; set; }
        public DateTime? PlanExpiresAt { get; set; }
        public virtual ICollection<FeatureUsageLog> FeatureUsageLogs { get; set; } = new List<FeatureUsageLog>();
    }
}
