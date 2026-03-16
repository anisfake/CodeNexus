using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AuditLogs.DTOs;
using CodeNexus.Domain.Entities;
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

        public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? httpContextAccessor = null, IAuditLogNotifier? auditLogNotifier = null) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
            _auditLogNotifier = auditLogNotifier;
        }

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Subject> Subjects => Set<Subject>();
        public DbSet<Goals> Goals => Set<Goals>();
        public DbSet<GoalMapping> GoalMappings => Set<GoalMapping>();
        public DbSet<SubjectGoal> SubjectGoals => Set<SubjectGoal>();
        public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
        public DbSet<LearningPathGoal> LearningPathGoals => Set<LearningPathGoal>();
        public DbSet<Chapter> Chapters => Set<Chapter>();
        public DbSet<Lesson> Lessons => Set<Lesson>();
        public DbSet<Tasks> Tasks => Set<Tasks>();
        public DbSet<FocusSession> FocusSessions => Set<FocusSession>();
        public DbSet<DailyCheckins> DailyCheckins => Set<DailyCheckins>();
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<Tag> Tags => Set<Tag>();
        public DbSet<NoteTags> NoteTags => Set<NoteTags>();
        public DbSet<Resource> Resources => Set<Resource>();
        public DbSet<ResourcePage> ResourcePages => Set<ResourcePage>();
        public DbSet<AISummary> AISummaries => Set<AISummary>();
        public DbSet<Conversation> Conversations => Set<Conversation>();
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
            modelBuilder.Entity<Tasks>().HasKey(e => e.TaskId);
            modelBuilder.Entity<FocusSession>().HasKey(e => e.SessionId);
            modelBuilder.Entity<DailyCheckins>().HasKey(e => e.CheckinId);
            modelBuilder.Entity<Note>().HasKey(e => e.NoteId);
            modelBuilder.Entity<Tag>().HasKey(e => e.TagId);
            modelBuilder.Entity<Resource>().HasKey(e => e.ResourceId);
            modelBuilder.Entity<ResourcePage>().HasKey(e => e.ResourcePageId);
            modelBuilder.Entity<AISummary>().HasKey(e => e.SummaryId);
            modelBuilder.Entity<Quiz>().HasKey(e => e.QuizId);
            modelBuilder.Entity<Questions>().HasKey(e => e.QuestionId);
            modelBuilder.Entity<QuizAttempt>().HasKey(e => e.AttemptId);
            modelBuilder.Entity<Goals>().HasKey(e => e.GoalId);
            modelBuilder.Entity<GoalMapping>().HasKey(e => e.MappingId);
            modelBuilder.Entity<SubjectGoal>().HasKey(e => new { e.SubjectId, e.GoalId });
            modelBuilder.Entity<LearningPathGoal>().HasKey(e => new { e.PathId, e.GoalId });
            modelBuilder.Entity<TokenBlacklist>().HasKey(e => e.Id);
            modelBuilder.Entity<AIProviderConfig>().HasKey(e => e.ConfigId);
            modelBuilder.Entity<Conversation>().HasKey(e => e.ConversationId);
            modelBuilder.Entity<Message>().HasKey(e => e.MessageId);
            modelBuilder.Entity<DirectConversation>().HasKey(e => e.ConversationId);
            modelBuilder.Entity<DirectMessage>().HasKey(e => e.MessageId);
            modelBuilder.Entity<DirectMessageReceipt>().HasKey(e => e.ReceiptId);
            modelBuilder.Entity<LearningPathShare>().HasKey(e => e.ShareId);

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
            });

            modelBuilder.Entity<NoteTags>(entity =>
            {
                entity.HasKey(nt => new { nt.NoteId, nt.TagId });

                entity.HasOne(nt => nt.Note)
                      .WithMany(n => n.NoteTags)
                      .HasForeignKey(nt => nt.NoteId);

                entity.HasOne(nt => nt.Tag)
                      .WithMany(t => t.NoteTags)
                      .HasForeignKey(nt => nt.TagId);
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

            modelBuilder.Entity<DailyCheckins>(entity =>
            {
                entity.HasIndex(dc => new { dc.SessionId, dc.CheckinDate }).IsUnique();

                entity.HasOne(dc => dc.FocusSession)
                      .WithOne(fs => fs.DailyCheckin)
                      .HasForeignKey<DailyCheckins>(dc => dc.SessionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LearningPath>(entity =>
            {
                entity.Property(p => p.ComplexityLevel)
                      .HasConversion<string>();

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

            modelBuilder.Entity<AIProviderConfig>(entity =>
            {
                entity.HasKey(e => e.ConfigId);

                entity.HasIndex(e => new { e.UsageType, e.IsActive });

                entity.HasMany(e => e.Conversations)
                      .WithOne(c => c.Provider)
                      .HasForeignKey(c => c.ConfigId)
                      .OnDelete(DeleteBehavior.Restrict);
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

                entity.HasIndex(e => new { e.MentorId, e.StudentId })
                      .IsUnique();

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

                entity.HasOne(e => e.Conversation)
                      .WithMany(c => c.Messages)
                      .HasForeignKey(e => e.ConversationId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Sender)
                      .WithMany()
                      .HasForeignKey(e => e.SenderId)
                      .OnDelete(DeleteBehavior.NoAction);
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

                entity.HasIndex(e => new { e.StudentId, e.Status, e.SentAt });

                entity.HasOne(e => e.LearningPath)
                      .WithMany()
                      .HasForeignKey(e => e.PathId)
                      .OnDelete(DeleteBehavior.Cascade);

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
