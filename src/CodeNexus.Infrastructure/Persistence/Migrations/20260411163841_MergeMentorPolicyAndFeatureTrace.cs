using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeMentorPolicyAndFeatureTrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[MentorAiAccessPolicies]', N'U') IS NOT NULL
BEGIN
    DECLARE @MonthlyLimit INT;
    DECLARE @CooldownHours INT;
    DECLARE @UpdatedAt DATETIME2;

    SELECT TOP (1)
        @MonthlyLimit = [MentorPaidRequestsMonthlyLimit],
        @CooldownHours = [MentorDowngradeNotifyCooldownHours],
        @UpdatedAt = [UpdatedAt]
    FROM [MentorAiAccessPolicies]
    ORDER BY [UpdatedAt] DESC;

    IF @MonthlyLimit IS NOT NULL AND @CooldownHours IS NOT NULL
    BEGIN
        DECLARE @ConfigJson NVARCHAR(MAX);
        SET @ConfigJson = CONCAT(
            N'{""mentorPaidRequestsMonthlyLimit"":',
            @MonthlyLimit,
            N',""mentorDowngradeNotifyCooldownHours"":',
            @CooldownHours,
            N'}');

        IF EXISTS (SELECT 1 FROM [SystemRuntimePolicies] WHERE [PolicyKey] = N'mentor_ai_access_policy')
        BEGIN
            UPDATE [SystemRuntimePolicies]
            SET [Description] = N'Mentor AI access policy',
                [ConfigJson] = @ConfigJson,
                [IsActive] = 1,
                [UpdatedAt] = COALESCE(@UpdatedAt, SYSUTCDATETIME())
            WHERE [PolicyKey] = N'mentor_ai_access_policy';
        END
        ELSE
        BEGIN
            INSERT INTO [SystemRuntimePolicies]
                ([SystemRuntimePolicyId], [PolicyKey], [Description], [ConfigJson], [IsActive], [UpdatedAt])
            VALUES
                (NEWID(), N'mentor_ai_access_policy', N'Mentor AI access policy', @ConfigJson, 1, COALESCE(@UpdatedAt, SYSUTCDATETIME()));
        END
    END
END
");

            migrationBuilder.DropTable(
                name: "MentorAiAccessPolicies");

            migrationBuilder.AddColumn<Guid>(
                name: "ConfigId",
                table: "AIUsageLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageLogs_ConfigId_CreatedAt",
                table: "AIUsageLogs",
                columns: new[] { "ConfigId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_AIUsageLogs_AIProviderConfigs_ConfigId",
                table: "AIUsageLogs",
                column: "ConfigId",
                principalTable: "AIProviderConfigs",
                principalColumn: "ConfigId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(@"
INSERT INTO [FeatureUsageLogs] ([FeatureUsageLogId], [UserId], [FeatureKey], [CreatedAt])
SELECT NEWID(), [UserId], N'LearningPathCreation', [CreatedAt]
FROM [LearningPaths];

INSERT INTO [FeatureUsageLogs] ([FeatureUsageLogId], [UserId], [FeatureKey], [CreatedAt])
SELECT NEWID(), c.[UserId], N'TutorMessages', m.[CreatedAt]
FROM [Messages] m
INNER JOIN [Conversations] c ON c.[ConversationId] = m.[ConversationId]
WHERE m.[Content] LIKE N'USER:%';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIUsageLogs_AIProviderConfigs_ConfigId",
                table: "AIUsageLogs");

            migrationBuilder.DropIndex(
                name: "IX_AIUsageLogs_ConfigId_CreatedAt",
                table: "AIUsageLogs");

            migrationBuilder.DropColumn(
                name: "ConfigId",
                table: "AIUsageLogs");

            migrationBuilder.CreateTable(
                name: "MentorAiAccessPolicies",
                columns: table => new
                {
                    MentorAiAccessPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorDowngradeNotifyCooldownHours = table.Column<int>(type: "int", nullable: false),
                    MentorPaidRequestsMonthlyLimit = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorAiAccessPolicies", x => x.MentorAiAccessPolicyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorAiAccessPolicies_UpdatedAt",
                table: "MentorAiAccessPolicies",
                column: "UpdatedAt");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [SystemRuntimePolicies] WHERE [PolicyKey] = N'mentor_ai_access_policy')
BEGIN
    DECLARE @MonthlyLimit INT = TRY_CAST(JSON_VALUE((SELECT TOP (1) [ConfigJson] FROM [SystemRuntimePolicies] WHERE [PolicyKey] = N'mentor_ai_access_policy'), '$.mentorPaidRequestsMonthlyLimit') AS INT);
    DECLARE @CooldownHours INT = TRY_CAST(JSON_VALUE((SELECT TOP (1) [ConfigJson] FROM [SystemRuntimePolicies] WHERE [PolicyKey] = N'mentor_ai_access_policy'), '$.mentorDowngradeNotifyCooldownHours') AS INT);
    DECLARE @UpdatedAt DATETIME2 = (SELECT TOP (1) [UpdatedAt] FROM [SystemRuntimePolicies] WHERE [PolicyKey] = N'mentor_ai_access_policy');

    INSERT INTO [MentorAiAccessPolicies]
        ([MentorAiAccessPolicyId], [MentorDowngradeNotifyCooldownHours], [MentorPaidRequestsMonthlyLimit], [UpdatedAt])
    VALUES
        (NEWID(), COALESCE(@CooldownHours, 24), COALESCE(@MonthlyLimit, 5000), COALESCE(@UpdatedAt, SYSUTCDATETIME()));
END
");
        }
    }
}
