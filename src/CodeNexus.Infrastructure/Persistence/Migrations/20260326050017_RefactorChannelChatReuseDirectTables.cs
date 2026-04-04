using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorChannelChatReuseDirectTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only drop table if it exists
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[dbo].[ChannelMessages]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [ChannelMessages]
                END
            ");

            migrationBuilder.DropIndex(
                name: "IX_DirectConversations_MentorId_StudentId",
                table: "DirectConversations");

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "DirectConversations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "MentorId",
                table: "DirectConversations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "DirectConversations",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConversationType",
                table: "DirectConversations",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "DirectConversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_MentorId_StudentId",
                table: "DirectConversations",
                columns: new[] { "MentorId", "StudentId" },
                unique: true,
                filter: "[ConversationType] = 'Direct' AND [MentorId] IS NOT NULL AND [StudentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_SubjectId_Category_ConversationType",
                table: "DirectConversations",
                columns: new[] { "SubjectId", "Category", "ConversationType" },
                unique: true,
                filter: "[ConversationType] = 'Channel' AND [SubjectId] IS NOT NULL AND [Category] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectConversations_Subjects_SubjectId",
                table: "DirectConversations",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "SubjectId",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectConversations_Subjects_SubjectId",
                table: "DirectConversations");

            migrationBuilder.DropIndex(
                name: "IX_DirectConversations_MentorId_StudentId",
                table: "DirectConversations");

            migrationBuilder.DropIndex(
                name: "IX_DirectConversations_SubjectId_Category_ConversationType",
                table: "DirectConversations");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "DirectConversations");

            migrationBuilder.DropColumn(
                name: "ConversationType",
                table: "DirectConversations");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "DirectConversations");

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentId",
                table: "DirectConversations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "MentorId",
                table: "DirectConversations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ChannelMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMessages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_ChannelMessages_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChannelMessages_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_MentorId_StudentId",
                table: "DirectConversations",
                columns: new[] { "MentorId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_SenderId",
                table: "ChannelMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_SubjectId_Category_SentAt",
                table: "ChannelMessages",
                columns: new[] { "SubjectId", "Category", "SentAt" });
        }
    }
}
