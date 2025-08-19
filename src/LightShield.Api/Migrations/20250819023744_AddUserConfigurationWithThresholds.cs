using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightShield.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserConfigurationWithThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxFailedLogins",
                table: "Configurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxFileCreates",
                table: "Configurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxFileDeletes",
                table: "Configurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxFileModifies",
                table: "Configurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Configurations",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxFailedLogins",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "MaxFileCreates",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "MaxFileDeletes",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "MaxFileModifies",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Configurations");
        }
    }
}
