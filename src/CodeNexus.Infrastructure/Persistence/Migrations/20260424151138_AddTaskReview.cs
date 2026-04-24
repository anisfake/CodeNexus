using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TaskReviewId",
                table: "DirectMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TaskReviews",
                columns: table => new
                {
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    Feedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Suggestions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StudentRequestNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskReviews", x => x.ReviewId);
                    table.ForeignKey(
                        name: "FK_TaskReviews_FocusSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "FocusSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskReviews_StudentMentorSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "StudentMentorSubscriptions",
                        principalColumn: "SubscriptionId");
                    table.ForeignKey(
                        name: "FK_TaskReviews_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "TaskId");
                    table.ForeignKey(
                        name: "FK_TaskReviews_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_TaskReviews_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_TaskReviewId",
                table: "DirectMessages",
                column: "TaskReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskReviews_MentorId_Status",
                table: "TaskReviews",
                columns: new[] { "MentorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskReviews_SessionId",
                table: "TaskReviews",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskReviews_StudentId",
                table: "TaskReviews",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskReviews_SubscriptionId",
                table: "TaskReviews",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskReviews_TaskId",
                table: "TaskReviews",
                column: "TaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectMessages_TaskReviews_TaskReviewId",
                table: "DirectMessages",
                column: "TaskReviewId",
                principalTable: "TaskReviews",
                principalColumn: "ReviewId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectMessages_TaskReviews_TaskReviewId",
                table: "DirectMessages");

            migrationBuilder.DropTable(
                name: "TaskReviews");

            migrationBuilder.DropIndex(
                name: "IX_DirectMessages_TaskReviewId",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "TaskReviewId",
                table: "DirectMessages");
        }
    }
}
