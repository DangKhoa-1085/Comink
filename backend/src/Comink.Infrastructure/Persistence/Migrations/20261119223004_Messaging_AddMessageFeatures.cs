using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Messaging_AddMessageFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "edited_at",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "forwarded_from_message_id",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reply_to_message_id",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "muted_until",
                table: "conversation_members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "message_reactions",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emoji = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_reactions", x => new { x.message_id, x.user_id, x.emoji });
                    table.ForeignKey(
                        name: "fk_message_reactions_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_message_reactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pinned_messages",
                columns: table => new
                {
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    pinned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pinned_messages", x => new { x.conversation_id, x.message_id });
                    table.ForeignKey(
                        name: "fk_pinned_messages_asp_net_users_pinned_by",
                        column: x => x.pinned_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pinned_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pinned_messages_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_messages_forwarded_from_message_id",
                table: "messages",
                column: "forwarded_from_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_reply_to_message_id",
                table: "messages",
                column: "reply_to_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_reactions_user_id",
                table: "message_reactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_pinned_messages_conversation_id_pinned_at",
                table: "pinned_messages",
                columns: new[] { "conversation_id", "pinned_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_pinned_messages_message_id",
                table: "pinned_messages",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_pinned_messages_pinned_by",
                table: "pinned_messages",
                column: "pinned_by");

            // Tìm kiếm tin nhắn không dấu (NC Ưu tiên 2 — quyết định D5): trigram trên nội dung
            // đã bỏ dấu + chữ thường, dùng lại hàm IMMUTABLE của migration M0.
            // EF Core không tạo được chỉ mục biểu thức nên dùng SQL trực tiếp (như posts).
            migrationBuilder.Sql(
                "CREATE INDEX ix_messages_content_trgm " +
                "ON messages USING gin (immutable_unaccent(lower(content)) gin_trgm_ops);");

            migrationBuilder.AddForeignKey(
                name: "fk_messages_messages_forwarded_from_message_id",
                table: "messages",
                column: "forwarded_from_message_id",
                principalTable: "messages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_messages_messages_reply_to_message_id",
                table: "messages",
                column: "reply_to_message_id",
                principalTable: "messages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_messages_content_trgm;");

            migrationBuilder.DropForeignKey(
                name: "fk_messages_messages_forwarded_from_message_id",
                table: "messages");

            migrationBuilder.DropForeignKey(
                name: "fk_messages_messages_reply_to_message_id",
                table: "messages");

            migrationBuilder.DropTable(
                name: "message_reactions");

            migrationBuilder.DropTable(
                name: "pinned_messages");

            migrationBuilder.DropIndex(
                name: "ix_messages_forwarded_from_message_id",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "ix_messages_reply_to_message_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "edited_at",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "forwarded_from_message_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "reply_to_message_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "muted_until",
                table: "conversation_members");
        }
    }
}
