using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Calls_InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "call_id",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "calls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initiator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "Audio"),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Ringing"),
                    room_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    end_reason = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calls", x => x.id);
                    table.CheckConstraint("ck_calls_duration", "duration_seconds IS NULL OR duration_seconds >= 0");
                    table.CheckConstraint("ck_calls_end_reason", "end_reason IS NULL OR end_reason IN ('Completed', 'NoAnswer', 'Declined', 'Cancelled', 'Failed')");
                    table.CheckConstraint("ck_calls_status", "status IN ('Ringing', 'InProgress', 'Completed', 'Declined', 'Cancelled', 'Missed', 'Failed')");
                    table.CheckConstraint("ck_calls_type", "type IN ('Audio', 'Video')");
                    table.ForeignKey(
                        name: "fk_calls_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calls_users_initiator_id",
                        column: x => x.initiator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "call_participants",
                columns: table => new
                {
                    call_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Invited"),
                    invited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_call_participants", x => new { x.call_id, x.user_id });
                    table.CheckConstraint("ck_call_participants_status", "status IN ('Invited', 'Joined', 'Declined', 'Missed', 'Left')");
                    table.ForeignKey(
                        name: "fk_call_participants_calls_call_id",
                        column: x => x.call_id,
                        principalTable: "calls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_call_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_messages_call_id",
                table: "messages",
                column: "call_id");

            migrationBuilder.CreateIndex(
                name: "ix_call_participants_user_id_invited_at",
                table: "call_participants",
                columns: new[] { "user_id", "invited_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_calls_conversation_id_created_at",
                table: "calls",
                columns: new[] { "conversation_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_calls_initiator_id",
                table: "calls",
                column: "initiator_id");

            migrationBuilder.CreateIndex(
                name: "ix_calls_room_name",
                table: "calls",
                column: "room_name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_messages_calls_call_id",
                table: "messages",
                column: "call_id",
                principalTable: "calls",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_messages_calls_call_id",
                table: "messages");

            migrationBuilder.DropTable(
                name: "call_participants");

            migrationBuilder.DropTable(
                name: "calls");

            migrationBuilder.DropIndex(
                name: "ix_messages_call_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "call_id",
                table: "messages");
        }
    }
}
