using CodeNexus.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        public DbSet<Role> Roles { get; }
        public DbSet<User> Users { get; }
        public DbSet<UserProfile> UserProfiles { get; }
        public DbSet<AuditLog> AuditLogs { get; }
        public DbSet<Notification> Notifications { get; }
        public DbSet<RefreshToken> RefreshTokens { get; }
        public DbSet<Subject> Subjects { get; }
        public DbSet<Goals> Goals { get; }
        public DbSet<LearningPath> LearningPaths { get; }
        public DbSet<Chapter> Chapters { get; }
        public DbSet<Lesson> Lessons { get; }
        public DbSet<Tasks> Tasks { get; }
        public DbSet<FocusSession> FocusSessions { get; }
        public DbSet<DailyCheckins> DailyCheckins { get; }
        public DbSet<Note> Notes { get; }
        public DbSet<Tag> Tags { get; }
        public DbSet<NoteTags> NoteTags { get; }
        public DbSet<Resource> Resources { get; }
        public DbSet<AISummary> AISummaries { get; }
        public DbSet<AIInteraction> AIInteractions { get; }
        public DbSet<ChatMessages> ChatMessages { get; }
        public DbSet<Quiz> Quizzes { get; }
        public DbSet<Questions> Questions { get; }
        public DbSet<QuizAttempt> QuizAttempts { get; }
        public DbSet<OtpVerification> OtpVerification { get; }
        public DbSet<TokenBlacklist> TokenBlacklist { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
