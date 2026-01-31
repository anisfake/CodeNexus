IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [AIInteractions] (
        [InteractionId] uniqueidentifier NOT NULL,
        [ModelName] nvarchar(max) NULL,
        [Version] nvarchar(max) NULL,
        [Configuration] nvarchar(max) NULL,
        [Query] nvarchar(max) NOT NULL,
        [Response] nvarchar(max) NULL,
        [Context] nvarchar(max) NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT [PK_AIInteractions] PRIMARY KEY ([InteractionId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [RoleId] uniqueidentifier NOT NULL,
        [RoleName] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Tags] (
        [TagId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Tags] PRIMARY KEY ([TagId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [UserId] uniqueidentifier NOT NULL,
        [Username] nvarchar(450) NOT NULL,
        [Email] nvarchar(450) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [FirstName] nvarchar(max) NULL,
        [LastName] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLogin] datetime2 NULL,
        [Status] nvarchar(max) NULL,
        [RoleId] uniqueidentifier NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_Users_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [LogId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Action] nvarchar(max) NOT NULL,
        [TableName] nvarchar(max) NULL,
        [RecordId] uniqueidentifier NULL,
        [OldValue] nvarchar(max) NULL,
        [NewValue] nvarchar(max) NULL,
        [Timestamp] datetime2 NOT NULL,
        [IPAddress] nvarchar(max) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([LogId]),
        CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [ChatMessages] (
        [MessageId] uniqueidentifier NOT NULL,
        [InteractionId] uniqueidentifier NULL,
        [UserId] uniqueidentifier NULL,
        [Sender] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([MessageId]),
        CONSTRAINT [FK_ChatMessages_AIInteractions_InteractionId] FOREIGN KEY ([InteractionId]) REFERENCES [AIInteractions] ([InteractionId]),
        CONSTRAINT [FK_ChatMessages_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Goals] (
        [GoalId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [TargetDate] datetime2 NULL,
        [IsCompleted] bit NOT NULL,
        [CompletedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Goals] PRIMARY KEY ([GoalId]),
        CONSTRAINT [FK_Goals_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [NotificationId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Message] nvarchar(max) NULL,
        [Type] nvarchar(max) NULL,
        [IsRead] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ReadAt] datetime2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([NotificationId]),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [TokenId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Token] nvarchar(max) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([TokenId]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Subjects] (
        [SubjectId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Color] nvarchar(max) NULL,
        [Icon] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Subjects] PRIMARY KEY ([SubjectId]),
        CONSTRAINT [FK_Subjects_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [UserProfiles] (
        [ProfileId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Bio] nvarchar(max) NULL,
        [AvatarUrl] nvarchar(max) NULL,
        [DateOfBirth] datetime2 NULL,
        [Phone] nvarchar(max) NULL,
        [Address] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_UserProfiles] PRIMARY KEY ([ProfileId]),
        CONSTRAINT [FK_UserProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [LearningPaths] (
        [PathId] uniqueidentifier NOT NULL,
        [SubjectId] uniqueidentifier NOT NULL,
        [GoalId] uniqueidentifier NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [StartDate] datetime2 NULL,
        [EndDate] datetime2 NULL,
        [Status] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByType] bit NOT NULL,
        CONSTRAINT [PK_LearningPaths] PRIMARY KEY ([PathId]),
        CONSTRAINT [FK_LearningPaths_Goals_GoalId] FOREIGN KEY ([GoalId]) REFERENCES [Goals] ([GoalId]),
        CONSTRAINT [FK_LearningPaths_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([SubjectId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Resources] (
        [ResourceId] uniqueidentifier NOT NULL,
        [SubjectId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [URL] nvarchar(max) NULL,
        [FilePath] nvarchar(max) NULL,
        [Description] nvarchar(max) NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Resources] PRIMARY KEY ([ResourceId]),
        CONSTRAINT [FK_Resources_Subjects_SubjectId] FOREIGN KEY ([SubjectId]) REFERENCES [Subjects] ([SubjectId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Chapters] (
        [ChapterId] uniqueidentifier NOT NULL,
        [PathId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NULL,
        [OrderIndex] int NOT NULL,
        [IsCompleted] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Chapters] PRIMARY KEY ([ChapterId]),
        CONSTRAINT [FK_Chapters_LearningPaths_PathId] FOREIGN KEY ([PathId]) REFERENCES [LearningPaths] ([PathId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [AISummaries] (
        [SummaryId] uniqueidentifier NOT NULL,
        [ModelName] nvarchar(max) NULL,
        [Version] nvarchar(max) NULL,
        [Configuration] nvarchar(max) NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Summary] nvarchar(max) NULL,
        [GeneratedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AISummaries] PRIMARY KEY ([SummaryId]),
        CONSTRAINT [FK_AISummaries_Resources_ResourceId] FOREIGN KEY ([ResourceId]) REFERENCES [Resources] ([ResourceId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Lessons] (
        [LessonId] uniqueidentifier NOT NULL,
        [ChapterId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [OrderIndex] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Lessons] PRIMARY KEY ([LessonId]),
        CONSTRAINT [FK_Lessons_Chapters_ChapterId] FOREIGN KEY ([ChapterId]) REFERENCES [Chapters] ([ChapterId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Tasks] (
        [TaskId] uniqueidentifier NOT NULL,
        [ChapterId] uniqueidentifier NOT NULL,
        [PathId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [DueDate] datetime2 NULL,
        [Priority] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        CONSTRAINT [PK_Tasks] PRIMARY KEY ([TaskId]),
        CONSTRAINT [FK_Tasks_Chapters_ChapterId] FOREIGN KEY ([ChapterId]) REFERENCES [Chapters] ([ChapterId]) ON DELETE CASCADE,
        CONSTRAINT [FK_Tasks_LearningPaths_PathId] FOREIGN KEY ([PathId]) REFERENCES [LearningPaths] ([PathId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Quizzes] (
        [QuizId] uniqueidentifier NOT NULL,
        [LessonId] uniqueidentifier NULL,
        [SummaryId] uniqueidentifier NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [TimeLimit] int NULL,
        [PassingScore] decimal(5,2) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Quizzes] PRIMARY KEY ([QuizId]),
        CONSTRAINT [FK_Quizzes_AISummaries_SummaryId] FOREIGN KEY ([SummaryId]) REFERENCES [AISummaries] ([SummaryId]),
        CONSTRAINT [FK_Quizzes_Lessons_LessonId] FOREIGN KEY ([LessonId]) REFERENCES [Lessons] ([LessonId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [FocusSessions] (
        [SessionId] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NULL,
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NULL,
        [Duration] int NOT NULL,
        [SessionType] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_FocusSessions] PRIMARY KEY ([SessionId]),
        CONSTRAINT [FK_FocusSessions_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [Tasks] ([TaskId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [TaskGoals] (
        [TaskGoalId] uniqueidentifier NOT NULL,
        [TaskId] uniqueidentifier NOT NULL,
        [Deadline] datetime2 NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TaskGoals] PRIMARY KEY ([TaskGoalId]),
        CONSTRAINT [FK_TaskGoals_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [Tasks] ([TaskId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Questions] (
        [QuestionId] uniqueidentifier NOT NULL,
        [QuizId] uniqueidentifier NOT NULL,
        [QuestionText] nvarchar(max) NOT NULL,
        [QuestionType] nvarchar(max) NULL,
        [Options] nvarchar(max) NULL,
        [CorrectAnswer] nvarchar(max) NULL,
        [Points] decimal(5,2) NOT NULL,
        [OrderIndex] int NULL,
        CONSTRAINT [PK_Questions] PRIMARY KEY ([QuestionId]),
        CONSTRAINT [FK_Questions_Quizzes_QuizId] FOREIGN KEY ([QuizId]) REFERENCES [Quizzes] ([QuizId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [QuizAttempts] (
        [AttemptId] uniqueidentifier NOT NULL,
        [QuizId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [StartTime] datetime2 NOT NULL,
        [EndTime] datetime2 NULL,
        [Score] decimal(5,2) NULL,
        [Status] nvarchar(max) NOT NULL,
        [Answers] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_QuizAttempts] PRIMARY KEY ([AttemptId]),
        CONSTRAINT [FK_QuizAttempts_Quizzes_QuizId] FOREIGN KEY ([QuizId]) REFERENCES [Quizzes] ([QuizId]) ON DELETE CASCADE,
        CONSTRAINT [FK_QuizAttempts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [DailyCheckins] (
        [CheckinId] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [CheckinDate] datetime2 NOT NULL,
        [Mood] nvarchar(max) NULL,
        [Productivity] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DailyCheckins] PRIMARY KEY ([CheckinId]),
        CONSTRAINT [FK_DailyCheckins_FocusSessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [FocusSessions] ([SessionId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [FocusGoals] (
        [FocusGoalId] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [Deadline] datetime2 NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_FocusGoals] PRIMARY KEY ([FocusGoalId]),
        CONSTRAINT [FK_FocusGoals_FocusSessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [FocusSessions] ([SessionId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [Notes] (
        [NoteId] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NULL,
        [FocusSessionSessionId] uniqueidentifier NULL,
        [Title] nvarchar(max) NULL,
        [Content] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Notes] PRIMARY KEY ([NoteId]),
        CONSTRAINT [FK_Notes_FocusSessions_FocusSessionSessionId] FOREIGN KEY ([FocusSessionSessionId]) REFERENCES [FocusSessions] ([SessionId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE TABLE [NoteTags] (
        [NoteId] uniqueidentifier NOT NULL,
        [TagId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_NoteTags] PRIMARY KEY ([NoteId], [TagId]),
        CONSTRAINT [FK_NoteTags_Notes_NoteId] FOREIGN KEY ([NoteId]) REFERENCES [Notes] ([NoteId]) ON DELETE CASCADE,
        CONSTRAINT [FK_NoteTags_Tags_TagId] FOREIGN KEY ([TagId]) REFERENCES [Tags] ([TagId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AISummaries_ResourceId] ON [AISummaries] ([ResourceId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Chapters_PathId] ON [Chapters] ([PathId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_InteractionId] ON [ChatMessages] ([InteractionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ChatMessages_UserId] ON [ChatMessages] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DailyCheckins_SessionId] ON [DailyCheckins] ([SessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DailyCheckins_SessionId_CheckinDate] ON [DailyCheckins] ([SessionId], [CheckinDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FocusGoals_SessionId] ON [FocusGoals] ([SessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FocusSessions_TaskId] ON [FocusSessions] ([TaskId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Goals_UserId] ON [Goals] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LearningPaths_GoalId] ON [LearningPaths] ([GoalId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LearningPaths_SubjectId] ON [LearningPaths] ([SubjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Lessons_ChapterId] ON [Lessons] ([ChapterId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notes_FocusSessionSessionId] ON [Notes] ([FocusSessionSessionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_NoteTags_TagId] ON [NoteTags] ([TagId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Questions_QuizId] ON [Questions] ([QuizId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_QuizAttempts_QuizId] ON [QuizAttempts] ([QuizId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_QuizAttempts_UserId] ON [QuizAttempts] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Quizzes_LessonId] ON [Quizzes] ([LessonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Quizzes_SummaryId] ON [Quizzes] ([SummaryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Resources_SubjectId] ON [Resources] ([SubjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Subjects_UserId] ON [Subjects] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TaskGoals_TaskId] ON [TaskGoals] ([TaskId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tasks_ChapterId] ON [Tasks] ([ChapterId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tasks_PathId] ON [Tasks] ([PathId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserProfiles_UserId] ON [UserProfiles] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_RoleId] ON [Users] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126145916_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260126145916_InitialCreate', N'8.0.23');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126152121_UpdateSubjectResourceLearningPath'
)
BEGIN
    ALTER TABLE [Subjects] DROP CONSTRAINT [FK_Subjects_Users_UserId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126152121_UpdateSubjectResourceLearningPath'
)
BEGIN
    EXEC sp_rename N'[Subjects].[UserId]', N'CreatedByUserId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126152121_UpdateSubjectResourceLearningPath'
)
BEGIN
    EXEC sp_rename N'[Subjects].[IX_Subjects_UserId]', N'IX_Subjects_CreatedByUserId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126152121_UpdateSubjectResourceLearningPath'
)
BEGIN
    ALTER TABLE [Resources] ADD [UserId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126152121_UpdateSubjectResourceLearningPath'
)
BEGIN
    ALTER TABLE [LearningPaths] ADD [UserId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126152121_UpdateSubjectResourceLearningPath'
)
BEGIN
    CREATE INDEX [IX_Resources_UserId] ON [Resources] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126152121_UpdateSubjectResourceLearningPath'
)
BEGIN
    CREATE INDEX [IX_LearningPaths_UserId] ON [LearningPaths] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126152121_UpdateSubjectResourceLearningPath'
)
BEGIN
    ALTER TABLE [LearningPaths] ADD CONSTRAINT [FK_LearningPaths_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126152121_UpdateSubjectResourceLearningPath'
)
BEGIN
    ALTER TABLE [Resources] ADD CONSTRAINT [FK_Resources_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126152121_UpdateSubjectResourceLearningPath'
)
BEGIN
    ALTER TABLE [Subjects] ADD CONSTRAINT [FK_Subjects_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126152121_UpdateSubjectResourceLearningPath'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260126152121_UpdateSubjectResourceLearningPath', N'8.0.23');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126165146_AddOtpVerification'
)
BEGIN
    CREATE TABLE [OtpVerification] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(450) NOT NULL,
        [Username] nvarchar(max) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [OtpHash] nvarchar(max) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [AttemptCount] int NOT NULL,
        [ResendCount] int NOT NULL,
        [LastResendAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OtpVerification] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126165146_AddOtpVerification'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OtpVerification_Email] ON [OtpVerification] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260126165146_AddOtpVerification'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260126165146_AddOtpVerification', N'8.0.23');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127114823_AddFirstNameLastNameField'
)
BEGIN
    ALTER TABLE [OtpVerification] ADD [FirstName] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127114823_AddFirstNameLastNameField'
)
BEGIN
    ALTER TABLE [OtpVerification] ADD [LastName] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127114823_AddFirstNameLastNameField'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260127114823_AddFirstNameLastNameField', N'8.0.23');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260129070713_AddPurposeToOTPTable'
)
BEGIN
    ALTER TABLE [OtpVerification] ADD [Purpose] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260129070713_AddPurposeToOTPTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260129070713_AddPurposeToOTPTable', N'8.0.23');
END;
GO

COMMIT;
GO

