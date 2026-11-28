using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Social_AddLockets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_context",
                table: "media_assets");

            migrationBuilder.CreateTable(
                name: "locket_moments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caption = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    audience_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "AllFriends"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locket_moments", x => x.id);
                    table.CheckConstraint("ck_locket_moments_audience", "audience_type IN ('AllFriends', 'SelectedFriends')");
                    table.CheckConstraint("ck_locket_moments_caption_length", "caption IS NULL OR char_length(caption) <= 100");
                    table.CheckConstraint("ck_locket_moments_expiry", "expires_at > created_at");
                    table.ForeignKey(
                        name: "fk_locket_moments_media_assets_media_asset_id",
                        column: x => x.media_asset_id,
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_locket_moments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "locket_reactions",
                columns: table => new
                {
                    moment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emoji = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locket_reactions", x => new { x.moment_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_locket_reactions_locket_moments_moment_id",
                        column: x => x.moment_id,
                        principalTable: "locket_moments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_locket_reactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "locket_recipients",
                columns: table => new
                {
                    moment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_locket_recipients", x => new { x.moment_id, x.recipient_user_id });
                    table.ForeignKey(
                        name: "fk_locket_recipients_locket_moments_moment_id",
                        column: x => x.moment_id,
                        principalTable: "locket_moments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_locket_recipients_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_context",
                table: "media_assets",
                sql: "context IN ('Post', 'Message', 'Avatar', 'Cover', 'Album', 'Locket')");

            migrationBuilder.CreateIndex(
                name: "ix_locket_moments_author_id_created_at_id",
                table: "locket_moments",
                columns: new[] { "author_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_locket_moments_expires_at_deleted_at",
                table: "locket_moments",
                columns: new[] { "expires_at", "deleted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_locket_moments_media_asset_id",
                table: "locket_moments",
                column: "media_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_locket_reactions_user_id",
                table: "locket_reactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_locket_recipients_recipient_user_id",
                table: "locket_recipients",
                column: "recipient_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "locket_reactions");

            migrationBuilder.DropTable(
                name: "locket_recipients");

            migrationBuilder.DropTable(
                name: "locket_moments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_context",
                table: "media_assets");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_context",
                table: "media_assets",
                sql: "context IN ('Post', 'Message', 'Avatar', 'Cover', 'Album')");
        }
    }
}
