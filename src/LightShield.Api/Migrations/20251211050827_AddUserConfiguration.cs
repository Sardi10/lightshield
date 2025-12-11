using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightShield.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Configurations",
                table: "Configurations");

            migrationBuilder.RenameTable(
                name: "Configurations",
                newName: "UserConfiguration");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserConfiguration",
                table: "UserConfiguration",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserConfiguration",
                table: "UserConfiguration");

            migrationBuilder.RenameTable(
                name: "UserConfiguration",
                newName: "Configurations");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Configurations",
                table: "Configurations",
                column: "Id");
        }
    }
}
