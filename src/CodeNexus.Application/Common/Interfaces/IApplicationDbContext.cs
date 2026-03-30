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
        public DbSet<GoalMapping> GoalMappings { get; }
        public DbSet<SubjectGoal> SubjectGoals { get; }
        public DbSet<LearningPath> LearningPaths { get; }
        public DbSet<LearningPathGoal> LearningPathGoals { get; }
        public DbSet<Chapter> Chapters { get; }
        public DbSet<Lesson> Lessons { get; }
        public DbSet<LearnProgress> LearnProgresses { get; }
        public DbSet<Tasks> Tasks { get; }
        public DbSet<FocusSession> FocusSessions { get; }
        public DbSet<DailyCheckins> DailyCheckins { get; }
        public DbSet<Note> Notes { get; }
        public DbSet<Tag> Tags { get; }
        public DbSet<NoteTags> NoteTags { get; }
        public DbSet<Resource> Resources { get; }
        public DbSet<ResourcePage> ResourcePages { get; }
        public DbSet<AISummary> AISummaries { get; }
        public DbSet<Conversation> Conversations { get; }
        public DbSet<Message> Messages { get; }
        public DbSet<DirectConversation> DirectConversations { get; }
        public DbSet<DirectMessage> DirectMessages { get; }
        public DbSet<DirectMessageReceipt> DirectMessageReceipts { get; }
        public DbSet<LearningPathShare> LearningPathShares { get; }
        public DbSet<AIProviderConfig> AIProviderConfigs { get; }
        public DbSet<AIUsageLog> AIUsageLogs { get; }
        public DbSet<Quiz> Quizzes { get; }
        public DbSet<Questions> Questions { get; }
        public DbSet<QuizAttempt> QuizAttempts { get; }
        public DbSet<TokenBlacklist> TokenBlacklist { get; }
        public DbSet<Achievement> Achievements { get; }
        public DbSet<UserAchievement> UserAchievements { get; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; }
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; }
        public DbSet<SubscriptionPlanLimit> SubscriptionPlanLimits { get; }
        public DbSet<FeatureUsageLog> FeatureUsageLogs { get; }
        public DbSet<MentorAiAccessPolicy> MentorAiAccessPolicies { get; }
        void SetAuditUserId(Guid userId);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
