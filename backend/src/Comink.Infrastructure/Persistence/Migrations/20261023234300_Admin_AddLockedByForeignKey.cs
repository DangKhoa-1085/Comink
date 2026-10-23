using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Admin_AddLockedByForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_users_locked_by",
                table: "users",
                column: "locked_by");

            migrationBuilder.AddForeignKey(
                name: "fk_users_users_locked_by",
                table: "users",
                column: "locked_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_users_locked_by",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_locked_by",
                table: "users");
        }
    }
}
