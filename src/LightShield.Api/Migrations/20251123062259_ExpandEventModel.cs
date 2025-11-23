using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightShield.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExpandEventModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "Events",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IPAddress",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogonType",
                table: "Events",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperatingSystem",
                table: "Events",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "Events",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Events",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IPAddress",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "LogonType",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "OperatingSystem",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Events");
        }
    }
}
