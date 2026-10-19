using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Messaging_InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversation_members",
                columns: table => new
                {
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Member"),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_members", x => new { x.conversation_id, x.user_id });
                    table.CheckConstraint("ck_conversation_members_role", "role IN ('Owner', 'Admin', 'Member')");
                    table.ForeignKey(
                        name: "fk_conversation_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Direct"),
                    direct_key = table.Column<string>(type: "character varying(73)", maxLength: 73, nullable: true),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    last_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversations", x => x.id);
                    table.CheckConstraint("ck_conversations_direct_key", "(type = 'Direct') = (direct_key IS NOT NULL)");
                    table.CheckConstraint("ck_conversations_type", "type IN ('Direct', 'Group')");
                    table.ForeignKey(
                        name: "fk_conversations_asp_net_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Text"),
                    content = table.Column<string>(type: "text", nullable: true),
                    client_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.CheckConstraint("ck_messages_content_length", "content IS NULL OR char_length(content) <= 4000");
                    table.CheckConstraint("ck_messages_type", "type IN ('Text', 'Image', 'Video', 'File', 'System', 'Call')");
                    table.ForeignKey(
                        name: "fk_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_messages_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "message_attachments",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_attachments", x => new { x.message_id, x.media_id });
                    table.ForeignKey(
                        name: "fk_message_attachments_media_assets_media_id",
                        column: x => x.media_id,
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_message_attachments_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_members_user_id",
                table: "conversation_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_created_by",
                table: "conversations",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_last_message_at",
                table: "conversations",
                column: "last_message_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_conversations_last_message_id",
                table: "conversations",
                column: "last_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_attachments_media_id",
                table: "message_attachments",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_conversation_id_created_at_id",
                table: "messages",
                columns: new[] { "conversation_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_messages_sender_id_client_message_id",
                table: "messages",
                columns: new[] { "sender_id", "client_message_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_conversation_members_conversations_conversation_id",
                table: "conversation_members",
                column: "conversation_id",
                principalTable: "conversations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_conversations_messages_last_message_id",
                table: "conversations",
                column: "last_message_id",
                principalTable: "messages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // EF Core không tạo được chỉ mục CÓ ĐIỀU KIỆN (WHERE) → viết SQL thủ công
            // (docs/03_database_design.md mục 5.2):
            //   1) mỗi cặp người chỉ có một hội thoại riêng;
            //   2) tìm nhanh hội thoại còn hiệu lực của một người.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_conversations_direct_key " +
                "ON conversations (direct_key) WHERE type = 'Direct';");

            migrationBuilder.Sql(
                "CREATE INDEX ix_conversation_members_user_active " +
                "ON conversation_members (user_id) WHERE left_at IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Gỡ chỉ mục thủ công trước khi xoá bảng.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_conversations_direct_key;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_conversation_members_user_active;");

            migrationBuilder.DropForeignKey(
                name: "fk_messages_conversations_conversation_id",
                table: "messages");

            migrationBuilder.DropTable(
                name: "conversation_members");

            migrationBuilder.DropTable(
                name: "message_attachments");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "messages");
        }
    }
}
