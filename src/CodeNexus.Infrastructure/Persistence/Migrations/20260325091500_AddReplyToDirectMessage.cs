using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260325091500_AddReplyToDirectMessage")]
    public partial class AddReplyToDirectMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "DirectMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ReplyToMessageId",
                table: "DirectMessages",
                column: "ReplyToMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectMessages_DirectMessages_ReplyToMessageId",
                table: "DirectMessages",
                column: "ReplyToMessageId",
                principalTable: "DirectMessages",
                principalColumn: "MessageId",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectMessages_DirectMessages_ReplyToMessageId",
                table: "DirectMessages");

            migrationBuilder.DropIndex(
                name: "IX_DirectMessages_ReplyToMessageId",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "DirectMessages");
        }
    }
}
