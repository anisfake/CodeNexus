using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.Infrastructure.Persistence;

namespace CodeNexus.Seeder
{
    public static class AchievementSeeder
    {
        public static async Task SeedAchievementsAsync(AppDbContext context)
        {
            if (context.Achievements.Any())
            {
                Console.WriteLine("Achievements already exist. Skipping seeding.");
                return;
            }

            var achievements = new List<Achievement>
            {
                // Learning Milestones
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "First Steps", // Key: "first_steps"
                    Description = "Complete your first lesson",
                    Icon = "🎯",
                    Category = AchievementCategory.LearningMilestone,
                    Points = 50,
                    SortOrder = 1
                },
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Chapter Master", // Key: "chapter_master"
                    Description = "Complete your first chapter",
                    Icon = "📖",
                    Category = AchievementCategory.LearningMilestone,
                    Points = 200,
                    SortOrder = 2
                },
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Course Conqueror", // Key: "course_conqueror"
                    Description = "Complete your first learning path",
                    Icon = "�"",
                    Category = AchievementCategory.LearningMilestone,
                    Points = 500,
                    SortOrder = 3
                },
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Goal Crusher", // Key: "goal_crusher"
                    Description = "Complete your first goal",
                    Icon = "🎯",
                    Category = AchievementCategory.LearningMilestone,
                    Points = 150,
                    SortOrder = 4
                },

                // Focus Achievements
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Focused Learner", // Key: "focused_learner"
                    Description = "Complete your first focus session",
                    Icon = "🎯",
                    Category = AchievementCategory.Focus,
                    Points = 100,
                    SortOrder = 1
                },
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Deep Focus", // Key: "deep_focus"
                    Description = "Complete a 90+ minute focus session",
                    Icon = "🧠",
                    Category = AchievementCategory.Focus,
                    Points = 300,
                    SortOrder = 2
                },
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Early Bird", // Key: "early_bird"
                    Description = "Study between 5-8 AM",
                    Icon = "🌅",
                    Category = AchievementCategory.Focus,
                    Points = 200,
                    SortOrder = 3
                },
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Night Owl", // Key: "night_owl"
                    Description = "Study between 10 PM - 2 AM",
                    Icon = "🦉",
                    Category = AchievementCategory.Focus,
                    Points = 200,
                    SortOrder = 4
                },

                // Social Achievements
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Helpful", // Key: "helpful"
                    Description = "Share your first learning resource",
                    Icon = "🤝",
                    Category = AchievementCategory.Social,
                    Points = 150,
                    SortOrder = 1
                },

                // NEW: Easy to implement achievements
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Speed Demon", // Key: "speed_demon"
                    Description = "Complete a focus session 30+ minutes early",
                    Icon = "⚡",
                    Category = AchievementCategory.Focus,
                    Points = 250,
                    SortOrder = 5
                },
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Consistent", // Key: "consistent"
                    Description = "Complete focus sessions 3 days in a row",
                    Icon = "📅",
                    Category = AchievementCategory.Focus,
                    Points = 400,
                    SortOrder = 6
                },
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Perfectionist", // Key: "perfectionist"
                    Description = "Get 100% score on a task verification",
                    Icon = "💯",
                    Category = AchievementCategory.LearningMilestone,
                    Points = 300,
                    SortOrder = 5
                },
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Multi Tasker", // Key: "multi_tasker"
                    Description = "Have 3 active goals at the same time",
                    Icon = "🎪",
                    Category = AchievementCategory.LearningMilestone,
                    Points = 200,
                    SortOrder = 6
                },
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Weekend Warrior", // Key: "weekend_warrior"
                    Description = "Complete a focus session on weekend",
                    Icon = "🏋️",
                    Category = AchievementCategory.Focus,
                    Points = 150,
                    SortOrder = 7
                },

                // Special Achievements
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Pioneer", // Key: "pioneer"
                    Description = "One of the first 100 users",
                    Icon = "🏅",
                    Category = AchievementCategory.Special,
                    Points = 1000,
                    SortOrder = 1
                },
                new Achievement
                {
                    AchievementId = NewId.NextGuid(),
                    Name = "Profile Complete", // Key: "profile_complete"
                    Description = "Complete your user profile",
                    Icon = "✅",
                    Category = AchievementCategory.Special,
                    Points = 50,
                    SortOrder = 2
                }
            };

            context.Achievements.AddRange(achievements);
            await context.SaveChangesAsync();

            Console.WriteLine($"Seeded {achievements.Count} achievements successfully!");
        }
    }
}