using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectChatAndLearningPathShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectConversations",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastMessagePreview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectConversations", x => x.ConversationId);
                    table.ForeignKey(
                        name: "FK_DirectConversations_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_DirectConversations_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "LearningPathShares",
                columns: table => new
                {
                    ShareId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PathId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningPathShares", x => x.ShareId);
                    table.ForeignKey(
                        name: "FK_LearningPathShares_LearningPaths_PathId",
                        column: x => x.PathId,
                        principalTable: "LearningPaths",
                        principalColumn: "PathId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningPathShares_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_LearningPathShares_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "DirectMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectMessages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_DirectMessages_DirectConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "DirectConversations",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectMessages_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "DirectMessageReceipts",
                columns: table => new
                {
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SeenAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectMessageReceipts", x => x.ReceiptId);
                    table.ForeignKey(
                        name: "FK_DirectMessageReceipts_DirectMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "DirectMessages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectMessageReceipts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_MentorId_StudentId",
                table: "DirectConversations",
                columns: new[] { "MentorId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_StudentId",
                table: "DirectConversations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessageReceipts_MessageId_UserId",
                table: "DirectMessageReceipts",
                columns: new[] { "MessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessageReceipts_UserId",
                table: "DirectMessageReceipts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ConversationId_SentAt",
                table: "DirectMessages",
                columns: new[] { "ConversationId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_SenderId",
                table: "DirectMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathShares_MentorId",
                table: "LearningPathShares",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathShares_PathId",
                table: "LearningPathShares",
                column: "PathId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathShares_StudentId_Status_SentAt",
                table: "LearningPathShares",
                columns: new[] { "StudentId", "Status", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectMessageReceipts");

            migrationBuilder.DropTable(
                name: "LearningPathShares");

            migrationBuilder.DropTable(
                name: "DirectMessages");

            migrationBuilder.DropTable(
                name: "DirectConversations");
        }
    }
}
