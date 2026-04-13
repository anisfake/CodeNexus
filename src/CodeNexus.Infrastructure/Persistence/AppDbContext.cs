using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AuditLogs.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace CodeNexus.Infrastructure.Persistence
{
    public class AppDbContext : DbContext, IApplicationDbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly IAuditLogNotifier? _auditLogNotifier;
        private Guid? _manualUserId;

        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? httpContextAccessor = null, IAuditLogNotifier? auditLogNotifier = null) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
            _auditLogNotifier = auditLogNotifier;
        }

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

        public void SetAuditUserId(Guid userId) => _manualUserId = userId;

        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Subject> Subjects => Set<Subject>();
        public DbSet<Goals> Goals => Set<Goals>();
        public DbSet<UserGoalProgress> UserGoalProgresses => Set<UserGoalProgress>();
        public DbSet<GoalMapping> GoalMappings => Set<GoalMapping>();
        public DbSet<SubjectGoal> SubjectGoals => Set<SubjectGoal>();
        public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
        public DbSet<LearningPathGoal> LearningPathGoals => Set<LearningPathGoal>();
        public DbSet<Chapter> Chapters => Set<Chapter>();
        public DbSet<Lesson> Lessons => Set<Lesson>();
        public DbSet<LearnProgress> LearnProgresses => Set<LearnProgress>();
        public DbSet<Tasks> Tasks => Set<Tasks>();
        public DbSet<FocusSession> FocusSessions => Set<FocusSession>();
        public DbSet<DailyCheckins> DailyCheckins => Set<DailyCheckins>();
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<Resource> Resources => Set<Resource>();
        public DbSet<ResourcePage> ResourcePages => Set<ResourcePage>();
        public DbSet<AISummary> AISummaries => Set<AISummary>();
        public DbSet<Conversation> Conversations => Set<Conversation>();
        public DbSet<ConversationSummary> ConversationSummaries => Set<ConversationSummary>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<DirectConversation> DirectConversations => Set<DirectConversation>();
        public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
        public DbSet<DirectMessageReceipt> DirectMessageReceipts => Set<DirectMessageReceipt>();
        public DbSet<LearningPathShare> LearningPathShares => Set<LearningPathShare>();
        public DbSet<Quiz> Quizzes => Set<Quiz>();
        public DbSet<Questions> Questions => Set<Questions>();
        public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
        public DbSet<TokenBlacklist> TokenBlacklist => Set<TokenBlacklist>();
        public DbSet<AIProviderConfig> AIProviderConfigs => Set<AIProviderConfig>();
        public DbSet<AIUsageLog> AIUsageLogs => Set<AIUsageLog>();
        public DbSet<Achievement> Achievements => Set<Achievement>();
        public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
        public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
        public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
        public DbSet<SubscriptionPlanLimit> SubscriptionPlanLimits => Set<SubscriptionPlanLimit>();
        public DbSet<FeatureUsageLog> FeatureUsageLogs => Set<FeatureUsageLog>();
        public DbSet<SystemRuntimePolicy> SystemRuntimePolicies => Set<SystemRuntimePolicy>();
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var (completedEntries, pendingEntries) = OnBeforeSaveChanges();

            var result = await base.SaveChangesAsync(cancellationToken);

            if (pendingEntries.Count > 0)
            {
                await OnAfterSaveChanges(pendingEntries, cancellationToken);
            }

            await NotifyAuditLogEntries(completedEntries, cancellationToken);

            return result;
        }

        private (List<AuditEntry> Completed, List<AuditEntry> Pending) OnBeforeSaveChanges()
        {
            ChangeTracker.DetectChanges();

            var auditEntries = new List<AuditEntry>();
            var userId = GetCurrentUserId();
            var ipAddress = GetCurrentIPAddress();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry
                {
                    UserId = userId,
                    TableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                    Action = entry.State.ToString(),
                    IPAddress = ipAddress,
                    Entry = entry
                };

                foreach (var property in entry.Properties)
                {
                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    var propertyName = property.Metadata.Name;

                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.RecordId = property.CurrentValue is Guid guidValue ? guidValue : null;
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            break;

                        case EntityState.Deleted:
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                            {
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }

                if (entry.State != EntityState.Modified || auditEntry.OldValues.Count > 0)
                {
                    auditEntries.Add(auditEntry);
                }
            }

            var completed = auditEntries.Where(e => !e.HasTemporaryProperties).ToList();
            var pending = auditEntries.Where(e => e.HasTemporaryProperties).ToList();

            foreach (var auditEntry in completed)
            {
                AuditLogs.Add(auditEntry.ToAuditLog());
            }

            return (completed, pending);
        }

        private async Task OnAfterSaveChanges(List<AuditEntry> auditEntries, CancellationToken cancellationToken)
        {
            foreach (var auditEntry in auditEntries)
            {
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.RecordId = prop.CurrentValue is Guid guidValue ? guidValue : null;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }

                AuditLogs.Add(auditEntry.ToAuditLog());
            }

            await base.SaveChangesAsync(cancellationToken);

            await NotifyAuditLogEntries(auditEntries, cancellationToken);
        }

        private async Task NotifyAuditLogEntries(List<AuditEntry> entries, CancellationToken cancellationToken)
        {
            if (_auditLogNotifier == null || entries.Count == 0)
                return;

            var username = GetCurrentUsername();

            foreach (var entry in entries)
            {
                var auditLog = entry.ToAuditLog();
                var response = new AuditLogResponse(
                    auditLog.LogId,
                    auditLog.UserId,
                    username,
                    auditLog.Action,
                    auditLog.TableName,
                    auditLog.RecordId,
                    auditLog.OldValue,
                    auditLog.NewValue,
                    auditLog.Timestamp,
                    auditLog.IPAddress
                );

                await _auditLogNotifier.NotifyAsync(response, cancellationToken);
            }
        }

        private string? GetCurrentUsername()
        {
            return _httpContextAccessor?.HttpContext?.User
                .FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        }

        private Guid? GetCurrentUserId()
        {
            if (_manualUserId.HasValue) return _manualUserId.Value;

            var userIdClaim = _httpContextAccessor?.HttpContext?.User
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
                return userId;

            return null;
        }

        private string? GetCurrentIPAddress()
        {
            return _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().HasKey(e => e.UserId);
            modelBuilder.Entity<Role>().HasKey(e => e.RoleId);
            modelBuilder.Entity<UserProfile>().HasKey(e => e.ProfileId);
            modelBuilder.Entity<RefreshToken>().HasKey(e => e.TokenId);
            modelBuilder.Entity<AuditLog>().HasKey(e => e.LogId);
            modelBuilder.Entity<Notification>().HasKey(e => e.NotificationId);
            modelBuilder.Entity<Subject>().HasKey(e => e.SubjectId);
            modelBuilder.Entity<LearningPath>().HasKey(e => e.PathId);
            modelBuilder.Entity<Chapter>().HasKey(e => e.ChapterId);
            modelBuilder.Entity<Lesson>().HasKey(e => e.LessonId);
            modelBuilder.Entity<LearnProgress>().HasKey(e => e.ProgressId);
            modelBuilder.Entity<Tasks>().HasKey(e => e.TaskId);
            modelBuilder.Entity<FocusSession>().HasKey(e => e.SessionId);
            modelBuilder.Entity<DailyCheckins>().HasKey(e => e.CheckinId);
            modelBuilder.Entity<Note>().HasKey(e => e.NoteId);
            modelBuilder.Entity<Resource>().HasKey(e => e.ResourceId);
            modelBuilder.Entity<ResourcePage>().HasKey(e => e.ResourcePageId);
            modelBuilder.Entity<AISummary>().HasKey(e => e.SummaryId);
            modelBuilder.Entity<Quiz>().HasKey(e => e.QuizId);
            modelBuilder.Entity<Questions>().HasKey(e => e.QuestionId);
            modelBuilder.Entity<QuizAttempt>().HasKey(e => e.AttemptId);
            modelBuilder.Entity<Goals>().HasKey(e => e.GoalId);
            modelBuilder.Entity<UserGoalProgress>().HasKey(e => e.UserGoalProgressId);
            modelBuilder.Entity<GoalMapping>().HasKey(e => e.MappingId);
            modelBuilder.Entity<SubjectGoal>().HasKey(e => new { e.SubjectId, e.GoalId });
            modelBuilder.Entity<LearningPathGoal>().HasKey(e => new { e.PathId, e.GoalId });
            modelBuilder.Entity<TokenBlacklist>().HasKey(e => e.Id);
            modelBuilder.Entity<AIProviderConfig>().HasKey(e => e.ConfigId);
            modelBuilder.Entity<Conversation>().HasKey(e => e.ConversationId);
            modelBuilder.Entity<ConversationSummary>().HasKey(e => e.SummaryId);
            modelBuilder.Entity<Message>().HasKey(e => e.MessageId);
            modelBuilder.Entity<DirectConversation>().HasKey(e => e.ConversationId);
            modelBuilder.Entity<DirectMessage>().HasKey(e => e.MessageId);
            modelBuilder.Entity<DirectMessageReceipt>().HasKey(e => e.ReceiptId);
            modelBuilder.Entity<LearningPathShare>().HasKey(e => e.ShareId);
            modelBuilder.Entity<AIUsageLog>().HasKey(e => e.UsageLogId);
            modelBuilder.Entity<SubscriptionPlan>().HasKey(e => e.SubscriptionPlanId);
            modelBuilder.Entity<SubscriptionPlanLimit>().HasKey(e => e.SubscriptionPlanLimitId);
            modelBuilder.Entity<FeatureUsageLog>().HasKey(e => e.FeatureUsageLogId);
            modelBuilder.Entity<SystemRuntimePolicy>().HasKey(e => e.SystemRuntimePolicyId);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var primaryKey = entityType.FindPrimaryKey();
                if (primaryKey != null && primaryKey.Properties.Count == 1)
                {
                    var pkProperty = primaryKey.Properties[0];
                    if (pkProperty.ClrType == typeof(Guid))
                    {
                        modelBuilder.Entity(entityType.ClrType)
                            .Property(pkProperty.Name)
                            .ValueGeneratedOnAdd();
                    }
                }
            }

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.Username).IsUnique();

                entity.HasOne(u => u.UserProfile)
                      .WithOne(p => p.User)
                      .HasForeignKey<UserProfile>(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(u => u.SubscriptionPlan)
                      .WithMany()
                      .HasForeignKey(u => u.SubscriptionPlanId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Tasks>(entity =>
            {
                entity.Property(t => t.Priority)
                      .HasConversion<string>();

                entity.Property(t => t.Status)
                      .HasConversion<string>();

                entity.HasOne(t => t.Chapter)
                      .WithMany(c => c.Tasks)
                      .HasForeignKey(t => t.ChapterId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.LearningPath)
                      .WithMany(lp => lp.Tasks)
                      .HasForeignKey(t => t.PathId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<FocusSession>(entity =>
            {
                entity.HasIndex(e => new { e.SessionStatus, e.LastActivityAt });
            });

            modelBuilder.Entity<DailyCheckins>(entity =>
            {
                entity.HasIndex(dc => new { dc.UserId, dc.CheckinDate }).IsUnique();

                entity.HasOne(dc => dc.User)
                      .WithMany(u => u.DailyCheckins)
                      .HasForeignKey(dc => dc.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Note>(entity =>
            {
                entity.HasIndex(n => n.SessionId);

                entity.HasOne(n => n.FocusSession)
                      .WithMany(fs => fs.Notes)
                      .HasForeignKey(n => n.SessionId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<LearningPath>(entity =>
            {
                entity.Property(p => p.ComplexityLevel)
                      .HasConversion<string>();

                entity.Property(p => p.VersionNumber)
                      .HasPrecision(4, 1)
                      .HasDefaultValue(1.0m);

                entity.HasOne(p => p.Subject)
                      .WithMany(s => s.LearningPaths)
                      .HasForeignKey(p => p.SubjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.User)
                      .WithMany(u => u.LearningPaths)
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<LearningPathGoal>(entity =>
            {
                entity.Property(e => e.Weight)
                      .HasPrecision(5, 2);

                entity.HasOne(e => e.LearningPath)
                      .WithMany(lp => lp.LearningPathGoals)
                      .HasForeignKey(e => e.PathId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Goal)
                      .WithMany(g => g.LearningPathGoals)
                      .HasForeignKey(e => e.GoalId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Subject>()
                .HasOne(s => s.CreatedByUser)
                .WithMany(u => u.Subjects)
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SubjectGoal>(entity =>
            {
                entity.HasOne(sg => sg.Subject)
                      .WithMany(s => s.SubjectGoals)
                      .HasForeignKey(sg => sg.SubjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sg => sg.Goal)
                      .WithMany(g => g.SubjectGoals)
                      .HasForeignKey(sg => sg.GoalId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserGoalProgress>(entity =>
            {
                entity.Property(e => e.Status)
                      .HasConversion<string>();

                entity.HasIndex(e => new { e.UserId, e.GoalId, e.LearningPathId })
                      .IsUnique();

                entity.HasIndex(e => new { e.UserId, e.LastUpdatedAt });

                entity.HasOne(e => e.User)
                      .WithMany(u => u.UserGoalProgresses)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Goal)
                      .WithMany(g => g.UserGoalProgresses)
                      .HasForeignKey(e => e.GoalId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.LearningPath)
                      .WithMany(lp => lp.UserGoalProgresses)
                      .HasForeignKey(e => e.LearningPathId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Resource>(entity =>
            {
                entity.Property(r => r.Type)
                      .HasConversion<string>();

                entity.HasOne(r => r.Subject)
                      .WithMany(s => s.Resources)
                      .HasForeignKey(r => r.SubjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.User)
                      .WithMany(u => u.Resources)
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(r => r.Pages)
                      .WithOne(p => p.Resource)
                      .HasForeignKey(p => p.ResourceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ResourcePage>(entity =>
            {
                entity.HasOne(p => p.Resource)
                      .WithMany(r => r.Pages)
                      .HasForeignKey(p => p.ResourceId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(p => new { p.ResourceId, p.PageNumber }).IsUnique();
            });

            modelBuilder.Entity<Chapter>()
                .HasOne(c => c.LearningPath)
                .WithMany(lp => lp.Chapters)
                .HasForeignKey(c => c.PathId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Lesson>()
                .HasOne(l => l.Chapter)
                .WithMany(c => c.Lessons)
                .HasForeignKey(l => l.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LearnProgress>(entity =>
            {
                entity.ToTable("LearnProgress");

                entity.HasIndex(e => new { e.LessonId, e.UserId })
                      .IsUnique();

                entity.HasOne(e => e.Lesson)
                      .WithMany(l => l.LearnProgresses)
                      .HasForeignKey(e => e.LessonId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                      .WithMany(u => u.LearnProgresses)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<AISummary>()
               .HasOne(s => s.Resource)
               .WithMany(r => r.AISummaries)
               .HasForeignKey(s => s.ResourceId)
               .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Goals>(entity =>
            {
                entity.HasOne(g => g.CreatedByUser)
                      .WithMany(u => u.Goals)
                      .HasForeignKey(g => g.CreatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<GoalMapping>(entity =>
            {
                entity.Property(e => e.Confidence)
                      .HasPrecision(5, 2);

                entity.HasIndex(e => new { e.UserGoalId, e.SystemGoalId })
                      .IsUnique();

                entity.HasOne(e => e.UserGoal)
                      .WithMany(g => g.UserGoalMappings)
                      .HasForeignKey(e => e.UserGoalId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.SystemGoal)
                      .WithMany(g => g.SystemGoalMappings)
                      .HasForeignKey(e => e.SystemGoalId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Questions>(entity =>
            {
                entity.Property(q => q.Points)
                      .HasPrecision(5, 2);

                entity.Property(q => q.Type)
                      .HasConversion<string>();
            });

            modelBuilder.Entity<Quiz>()
                .Property(q => q.PassingScore)
                .HasPrecision(5, 2);

            modelBuilder.Entity<QuizAttempt>(entity =>
            {
                entity.Property(q => q.Score)
                      .HasPrecision(5, 2);

                entity.Property(qa => qa.Status)
                      .HasConversion<string>();
            });

            modelBuilder.Entity<Notification>()
                .Property(n => n.Type)
                .HasConversion<string>();

            modelBuilder.Entity<Notification>()
                .Property(n => n.NotifiedSourceVersion)
                .HasPrecision(4, 1);

            modelBuilder.Entity<AIProviderConfig>(entity =>
            {
                entity.HasKey(e => e.ConfigId);

                entity.Property(e => e.AccessTier)
                      .HasConversion<string>();

                entity.HasIndex(e => new { e.UsageType, e.AccessTier })
                      .IsUnique()
                      .HasFilter("[IsActive] = 1");

                entity.HasMany(e => e.Conversations)
                      .WithOne(c => c.Provider)
                      .HasForeignKey(c => c.ConfigId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AIUsageLog>(entity =>
            {
                entity.HasIndex(e => new { e.UsageType, e.CreatedAt });
                entity.HasIndex(e => new { e.AccessTierUsed, e.UsageType, e.CreatedAt });
                entity.HasIndex(e => new { e.ConfigId, e.CreatedAt });
                entity.Property(e => e.AccessTierUsed)
                    .HasConversion<string>();
                entity.Property(e => e.CostUsd).HasPrecision(18, 8);

                entity.HasOne(e => e.Config)
                      .WithMany(c => c.AIUsageLogs)
                      .HasForeignKey(e => e.ConfigId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                entity.HasKey(e => e.PaymentTransactionId);

                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.Status).HasConversion<string>();

                entity.HasIndex(e => e.TxnRef).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.SubscriptionPlan)
                      .WithMany()
                      .HasForeignKey(e => e.SubscriptionPlanId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<SubscriptionPlan>(entity =>
            {
                entity.Property(e => e.PlanType)
                      .HasConversion<string>();

                entity.Property(e => e.Name)
                      .HasMaxLength(120);

                entity.Property(e => e.Description)
                      .HasMaxLength(500);

                entity.Property(e => e.PriceVnd)
                      .HasPrecision(18, 2);

                entity.HasIndex(e => e.PlanType)
                      .IsUnique();

                entity.HasIndex(e => e.DisplayOrder);

            });

            modelBuilder.Entity<SubscriptionPlanLimit>(entity =>
            {
                entity.Property(e => e.FeatureKey)
                      .HasConversion<string>();

                entity.Property(e => e.WindowType)
                      .HasConversion<string>();

                entity.HasIndex(e => new { e.SubscriptionPlanId, e.FeatureKey })
                      .IsUnique();

                entity.HasOne(e => e.SubscriptionPlan)
                      .WithMany(p => p.Limits)
                      .HasForeignKey(e => e.SubscriptionPlanId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FeatureUsageLog>(entity =>
            {
                entity.Property(e => e.FeatureKey)
                      .HasConversion<string>();

                entity.HasIndex(e => new { e.UserId, e.FeatureKey, e.CreatedAt });

                entity.HasOne(e => e.User)
                      .WithMany(u => u.FeatureUsageLogs)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SystemRuntimePolicy>(entity =>
            {
                entity.Property(e => e.PolicyKey)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(e => e.Description)
                      .HasMaxLength(500);

                entity.Property(e => e.ConfigJson)
                      .IsRequired();

                entity.Property(e => e.IsActive)
                      .HasDefaultValue(true);

                entity.HasIndex(e => e.PolicyKey)
                      .IsUnique();

                entity.HasIndex(e => e.UpdatedAt);
            });

            modelBuilder.Entity<Conversation>(entity =>
            {
                entity.HasKey(e => e.ConversationId);

                entity.HasOne(c => c.User)
                      .WithMany(u => u.Conversations)
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Provider)
                      .WithMany(p => p.Conversations)
                      .HasForeignKey(c => c.ConfigId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(c => c.Messages)
                      .WithOne(m => m.Conversation)
                      .HasForeignKey(m => m.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Summaries)
                      .WithOne(s => s.Conversation)
                      .HasForeignKey(s => s.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ConversationSummary>(entity =>
            {
                entity.HasKey(e => e.SummaryId);

                entity.Property(e => e.SummaryContent)
                      .IsRequired();

                entity.HasIndex(e => new { e.ConversationId, e.CreatedAt });

                entity.HasOne(e => e.Conversation)
                      .WithMany(c => c.Summaries)
                      .HasForeignKey(e => e.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasKey(e => e.MessageId);

                entity.HasOne(m => m.Conversation)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(m => m.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DirectConversation>(entity =>
            {
                entity.HasKey(e => e.ConversationId);

                entity.Property(e => e.ConversationType)
                      .HasConversion<string>();

                entity.Property(e => e.Category)
                      .HasConversion<string>();

                entity.HasIndex(e => new { e.MentorId, e.StudentId })
                      .IsUnique()
                      .HasFilter("[ConversationType] = 'Direct' AND [MentorId] IS NOT NULL AND [StudentId] IS NOT NULL");

                entity.HasIndex(e => new { e.Category, e.ConversationType })
                      .IsUnique()
                      .HasFilter("[ConversationType] = 'Channel' AND [Category] IS NOT NULL");

                entity.HasOne(e => e.Mentor)
                      .WithMany()
                      .HasForeignKey(e => e.MentorId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Student)
                      .WithMany()
                      .HasForeignKey(e => e.StudentId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasMany(e => e.Messages)
                      .WithOne(m => m.Conversation)
                      .HasForeignKey(m => m.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DirectMessage>(entity =>
            {
                entity.HasKey(e => e.MessageId);

                entity.Property(e => e.MessageType)
                      .HasConversion<string>();

                entity.HasIndex(e => new { e.ConversationId, e.SentAt });
                entity.HasIndex(e => e.ReplyToMessageId);

                entity.HasOne(e => e.Conversation)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(e => e.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Sender)
                      .WithMany()
                      .HasForeignKey(e => e.SenderId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.ReplyToMessage)
                      .WithMany(e => e.Replies)
                      .HasForeignKey(e => e.ReplyToMessageId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.LearningPathShare)
                      .WithMany()
                      .HasForeignKey(e => e.LearningPathShareId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<DirectMessageReceipt>(entity =>
            {
                entity.HasKey(e => e.ReceiptId);

                entity.HasIndex(e => new { e.MessageId, e.UserId })
                      .IsUnique();

                entity.HasOne(e => e.Message)
                      .WithMany(m => m.Receipts)
                      .HasForeignKey(e => e.MessageId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<LearningPathShare>(entity =>
            {
                entity.HasKey(e => e.ShareId);

                entity.Property(e => e.Status)
                      .HasConversion<string>();

                entity.Property(e => e.IsTrackingEnabled)
                      .HasDefaultValue(true);

                entity.Property(e => e.InvalidatedReason)
                      .HasMaxLength(100);

                entity.Property(e => e.SourceVersionAtAccept)
                      .HasPrecision(4, 1);

                entity.Property(e => e.IgnoredSourceVersion)
                      .HasPrecision(4, 1);

                entity.Property(e => e.LastNotifiedSourceVersion)
                      .HasPrecision(4, 1);

                entity.HasIndex(e => new { e.StudentId, e.Status, e.SentAt });
                entity.HasIndex(e => e.AcceptedPathId);
                entity.HasIndex(e => new { e.PathId, e.StudentId, e.Status, e.IsTrackingEnabled });
                entity.HasIndex(e => new { e.PathId, e.MentorId, e.StudentId })
                      .IsUnique()
                      .HasFilter("[Status] = 'Pending'");

                entity.HasOne(e => e.LearningPath)
                      .WithMany()
                      .HasForeignKey(e => e.PathId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AcceptedPath)
                      .WithMany()
                      .HasForeignKey(e => e.AcceptedPathId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Mentor)
                      .WithMany()
                      .HasForeignKey(e => e.MentorId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Student)
                      .WithMany()
                      .HasForeignKey(e => e.StudentId)
                      .OnDelete(DeleteBehavior.NoAction);
            });
        }
    }
}
