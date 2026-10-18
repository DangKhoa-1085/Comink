using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Social_InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "follows",
                columns: table => new
                {
                    follower_id = table.Column<Guid>(type: "uuid", nullable: false),
                    followee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_follows", x => new { x.follower_id, x.followee_id });
                    table.CheckConstraint("ck_follows_not_self", "follower_id <> followee_id");
                    table.ForeignKey(
                        name: "fk_follows_users_followee_id",
                        column: x => x.followee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_follows_users_follower_id",
                        column: x => x.follower_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    addressee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Pending"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friendships", x => x.id);
                    table.CheckConstraint("ck_friendships_not_self", "requester_id <> addressee_id");
                    table.CheckConstraint("ck_friendships_status", "status IN ('Pending', 'Accepted')");
                    table.ForeignKey(
                        name: "fk_friendships_users_addressee_id",
                        column: x => x.addressee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_friendships_users_requester_id",
                        column: x => x.requester_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Cloudinary"),
                    provider_public_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    resource_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Image"),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    original_filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    duration_seconds = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    context = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Post"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.id);
                    table.CheckConstraint("ck_media_assets_context", "context IN ('Post', 'Message', 'Avatar', 'Cover', 'Album')");
                    table.CheckConstraint("ck_media_assets_provider", "provider IN ('Cloudinary', 'Firebase')");
                    table.CheckConstraint("ck_media_assets_resource_type", "resource_type IN ('Image', 'Video', 'File')");
                    table.CheckConstraint("ck_media_assets_size", "size_bytes > 0");
                    table.ForeignKey(
                        name: "fk_media_assets_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "posts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    visibility = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Friends"),
                    shared_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Active"),
                    like_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    comment_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    share_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_posts", x => x.id);
                    table.CheckConstraint("ck_posts_content_length", "char_length(content) <= 5000");
                    table.CheckConstraint("ck_posts_status", "status IN ('Active', 'Hidden')");
                    table.CheckConstraint("ck_posts_visibility", "visibility IN ('Public', 'Friends', 'OnlyMe')");
                    table.ForeignKey(
                        name: "fk_posts_posts_shared_post_id",
                        column: x => x.shared_post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_posts_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Active"),
                    reply_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comments", x => x.id);
                    table.CheckConstraint("ck_comments_content_length", "char_length(content) BETWEEN 1 AND 2000");
                    table.CheckConstraint("ck_comments_status", "status IN ('Active', 'Hidden')");
                    table.ForeignKey(
                        name: "fk_comments_comments_parent_comment_id",
                        column: x => x.parent_comment_id,
                        principalTable: "comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_comments_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "post_likes",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_post_likes", x => new { x.post_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_post_likes_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_post_likes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "post_media",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_post_media", x => new { x.post_id, x.media_id });
                    table.ForeignKey(
                        name: "fk_post_media_media_assets_media_id",
                        column: x => x.media_id,
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_post_media_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_comments_author_id",
                table: "comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_comments_parent_comment_id_created_at",
                table: "comments",
                columns: new[] { "parent_comment_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_comments_post_id_created_at_id",
                table: "comments",
                columns: new[] { "post_id", "created_at", "id" },
                filter: "deleted_at IS NULL AND parent_comment_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_follows_followee_id",
                table: "follows",
                column: "followee_id");

            migrationBuilder.CreateIndex(
                name: "ix_friendships_addressee_id_status",
                table: "friendships",
                columns: new[] { "addressee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_friendships_requester_id_status",
                table: "friendships",
                columns: new[] { "requester_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_owner_id",
                table: "media_assets",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_provider_provider_public_id",
                table: "media_assets",
                columns: new[] { "provider", "provider_public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_post_likes_user_id",
                table: "post_likes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_post_media_media_id",
                table: "post_media",
                column: "media_id");

            migrationBuilder.CreateIndex(
                name: "ix_posts_author_id_created_at_id",
                table: "posts",
                columns: new[] { "author_id", "created_at", "id" },
                descending: new[] { false, true, true },
                filter: "deleted_at IS NULL AND status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_posts_shared_post_id",
                table: "posts",
                column: "shared_post_id");

            // --- Chỉ mục biểu thức và cột sinh mà EF Core không khai báo được ---
            // (docs/03_database_design.md mục 5.2 và 5.3)

            // Mỗi cặp người dùng chỉ có một dòng bạn bè, bất kể ai gửi lời mời trước.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_friendships_pair
                    ON friendships (LEAST(requester_id, addressee_id), GREATEST(requester_id, addressee_id));
                """);

            // Tìm người dùng khi gõ không dấu (Giai đoạn 2): trigram trên tên hiển thị và tên đăng nhập.
            migrationBuilder.Sql("""
                CREATE INDEX ix_user_profiles_display_name_trgm
                    ON user_profiles USING gin (immutable_unaccent(lower(display_name)) gin_trgm_ops);
                """);

            migrationBuilder.Sql("""
                CREATE INDEX ix_users_normalized_user_name_trgm
                    ON users USING gin (normalized_user_name gin_trgm_ops);
                """);

            // Cột sinh phục vụ tìm bài viết bằng full-text (chưa dùng ở GĐ 2 nhưng thuộc
            // thiết kế bảng posts — mục 3.2). Không ánh xạ trong EF nên không cần khai báo model.
            migrationBuilder.Sql("""
                ALTER TABLE posts
                    ADD COLUMN search_vector tsvector
                    GENERATED ALWAYS AS (to_tsvector('simple', immutable_unaccent(coalesce(content, '')))) STORED;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX ix_posts_search_vector ON posts USING gin (search_vector);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Chỉ mục nằm trên bảng còn tồn tại (users, user_profiles) phải xóa tường minh;
            // các chỉ mục/cột còn lại mất theo bảng ở các lệnh DropTable bên dưới.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_posts_search_vector;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_users_normalized_user_name_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_user_profiles_display_name_trgm;");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "follows");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropTable(
                name: "post_likes");

            migrationBuilder.DropTable(
                name: "post_media");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "posts");
        }
    }
}
