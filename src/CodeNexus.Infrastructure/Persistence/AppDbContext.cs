using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;


namespace CodeNexus.Infrastructure.Persistence
{
    public class AppDbContext : DbContext, IApplicationDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Subject> Subjects => Set<Subject>();
        public DbSet<Goals> Goals => Set<Goals>();
        public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
        public DbSet<Chapter> Chapters => Set<Chapter>();
        public DbSet<Lesson> Lessons => Set<Lesson>();
        public DbSet<Tasks> Tasks => Set<Tasks>();
        public DbSet<FocusSession> FocusSessions => Set<FocusSession>();
        public DbSet<DailyCheckins> DailyCheckins => Set<DailyCheckins>();
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<Tag> Tags => Set<Tag>();
        public DbSet<NoteTags> NoteTags => Set<NoteTags>();
        public DbSet<Resource> Resources => Set<Resource>();
        public DbSet<AISummary> AISummaries => Set<AISummary>();
        public DbSet<AIInteraction> AIInteractions => Set<AIInteraction>();
        public DbSet<ChatMessages> ChatMessages => Set<ChatMessages>();
        public DbSet<Quiz> Quizzes => Set<Quiz>();
        public DbSet<Questions> Questions => Set<Questions>();
        public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
        public DbSet<OtpVerification> OtpVerification => Set<OtpVerification>();
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await base.SaveChangesAsync(cancellationToken);

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
            modelBuilder.Entity<AISummary>().HasKey(e => e.SummaryId);
            modelBuilder.Entity<AIInteraction>().HasKey(e => e.InteractionId);
            modelBuilder.Entity<ChatMessages>().HasKey(e => e.MessageId);
            modelBuilder.Entity<Quiz>().HasKey(e => e.QuizId);
            modelBuilder.Entity<Questions>().HasKey(e => e.QuestionId);
            modelBuilder.Entity<QuizAttempt>().HasKey(e => e.AttemptId);
            modelBuilder.Entity<Goals>().HasKey(e => e.GoalId);
            modelBuilder.Entity<OtpVerification>().HasKey(e => e.Id);

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
                entity.HasOne(p => p.Subject)
                      .WithMany(s => s.LearningPaths)
                      .HasForeignKey(p => p.SubjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.User)
                      .WithMany(u => u.LearningPaths)
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Subject>()
                .HasOne(s => s.CreatedByUser)
                .WithMany(u => u.Subjects)
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Resource>(entity =>
            {
                entity.HasOne(r => r.Subject)
                      .WithMany(s => s.Resources)
                      .HasForeignKey(r => r.SubjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.User)
                      .WithMany(u => u.Resources)
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.NoAction);
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
                entity.HasOne(g => g.User)
                      .WithMany(u => u.Goals)
                      .HasForeignKey(g => g.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Questions>()
                .Property(q => q.Points)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Quiz>()
                .Property(q => q.PassingScore)
                .HasPrecision(5, 2);

            modelBuilder.Entity<QuizAttempt>()
                .Property(q => q.Score)
                .HasPrecision(5, 2);

            modelBuilder.Entity<OtpVerification>(entity =>
            {
                entity.HasIndex(o => o.Email).IsUnique();
            });
        }
    }
}
