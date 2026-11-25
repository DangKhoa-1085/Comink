using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Media_AddLocalProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_provider",
                table: "media_assets");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_provider",
                table: "media_assets",
                sql: "provider IN ('Cloudinary', 'Firebase', 'Local')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_media_assets_provider",
                table: "media_assets");

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_assets_provider",
                table: "media_assets",
                sql: "provider IN ('Cloudinary', 'Firebase')");
        }
    }
}
