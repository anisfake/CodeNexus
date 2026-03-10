using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAISummaryQuizzesNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop shadow FK and column only if they exist (may not exist if DB was manually cleaned)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Quizzes_AISummaries_AISummarySummaryId')
                    ALTER TABLE [Quizzes] DROP CONSTRAINT [FK_Quizzes_AISummaries_AISummarySummaryId];

                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Quizzes') AND name = 'AISummarySummaryId')
                    ALTER TABLE [Quizzes] DROP COLUMN [AISummarySummaryId];
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AISummarySummaryId",
                table: "Quizzes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_AISummaries_AISummarySummaryId",
                table: "Quizzes",
                column: "AISummarySummaryId",
                principalTable: "AISummaries",
                principalColumn: "SummaryId");
        }
    }
}
