using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Messaging_AddReadMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_read_at",
                table: "conversation_members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_read_message_id",
                table: "conversation_members",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversation_members_last_read_message_id",
                table: "conversation_members",
                column: "last_read_message_id");

            migrationBuilder.AddForeignKey(
                name: "fk_conversation_members_messages_last_read_message_id",
                table: "conversation_members",
                column: "last_read_message_id",
                principalTable: "messages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_conversation_members_messages_last_read_message_id",
                table: "conversation_members");

            migrationBuilder.DropIndex(
                name: "ix_conversation_members_last_read_message_id",
                table: "conversation_members");

            migrationBuilder.DropColumn(
                name: "last_read_at",
                table: "conversation_members");

            migrationBuilder.DropColumn(
                name: "last_read_message_id",
                table: "conversation_members");
        }
    }
}
