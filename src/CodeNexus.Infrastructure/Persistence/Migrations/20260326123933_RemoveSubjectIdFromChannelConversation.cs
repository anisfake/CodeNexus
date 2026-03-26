using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSubjectIdFromChannelConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectConversations_Subjects_SubjectId",
                table: "DirectConversations");

            migrationBuilder.DropIndex(
                name: "IX_DirectConversations_SubjectId_Category_ConversationType",
                table: "DirectConversations");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "DirectConversations");

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_Category_ConversationType",
                table: "DirectConversations",
                columns: new[] { "Category", "ConversationType" },
                unique: true,
                filter: "[ConversationType] = 'Channel' AND [Category] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DirectConversations_Category_ConversationType",
                table: "DirectConversations");

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "DirectConversations",
                type: "uniqueidentifier",
                nullable: true);

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
                principalColumn: "SubjectId");
        }
    }
}
