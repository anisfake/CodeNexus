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
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Goals> Goals { get; set; }
        public DbSet<LearningPath> LearningPaths { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Lesson> Lessons { get; set; }

        public DbSet<Tasks> Tasks { get; set; }
        public DbSet<TaskGoals> TaskGoals { get; set; }
        public DbSet<FocusSession> FocusSessions { get; set; }
        public DbSet<FocusGoals> FocusGoals { get; set; }
        public DbSet<DailyCheckins> DailyCheckins { get; set; }

        public DbSet<Note> Notes { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<NoteTags> NoteTags { get; set; }

        public DbSet<Resource> Resources { get; set; }
        public DbSet<AISummary> AISummaries { get; set; }
        public DbSet<AIInteraction> AIInteractions { get; set; }
        public DbSet<ChatMessages> ChatMessages { get; set; }

        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Questions> Questions { get; set; }
        public DbSet<QuizAttempt> QuizAttempts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Primary Keys
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
            modelBuilder.Entity<FocusGoals>().HasKey(e => e.FocusGoalId);
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

            // Auto-generate Guid for all Guid primary keys
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

            modelBuilder.Entity<TaskGoals>(entity =>
            {
                entity.HasKey(tg => tg.TaskGoalId);
                entity.HasOne(tg => tg.Task)
                      .WithOne(t => t.TaskGoal)
                      .HasForeignKey<TaskGoals>(tg => tg.TaskId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FocusGoals>(entity =>
            {
                entity.HasOne(fg => fg.FocusSession)
                      .WithOne(fs => fs.FocusGoal)
                      .HasForeignKey<FocusGoals>(fg => fg.SessionId)
                      .OnDelete(DeleteBehavior.Cascade);
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
                      .OnDelete(DeleteBehavior.NoAction); // Avoid cascade cycle
            });

            modelBuilder.Entity<Subject>()
                .HasOne(s => s.CreatedByUser)
                .WithMany(u => u.Subjects)
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction); // Mentor created, don't cascade

            modelBuilder.Entity<Resource>(entity =>
            {
                entity.HasOne(r => r.Subject)
                      .WithMany(s => s.Resources)
                      .HasForeignKey(r => r.SubjectId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.User)
                      .WithMany(u => u.Resources)
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.NoAction); // User uploaded, don't cascade
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

            // Decimal precision
            modelBuilder.Entity<Questions>()
                .Property(q => q.Points)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Quiz>()
                .Property(q => q.PassingScore)
                .HasPrecision(5, 2);

            modelBuilder.Entity<QuizAttempt>()
                .Property(q => q.Score)
                .HasPrecision(5, 2);
        }
    }
}
