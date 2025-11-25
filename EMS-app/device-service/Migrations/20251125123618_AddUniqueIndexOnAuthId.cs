using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace device_service.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnAuthId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_AuthId",
                table: "Users",
                column: "AuthId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_AuthId",
                table: "Users");
        }
    }
}
