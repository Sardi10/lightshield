using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightShield.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBaselineDetectionGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DetectionEnabled",
                table: "FileActivityBaselines",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstSeen",
                table: "FileActivityBaselines",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectionEnabled",
                table: "FileActivityBaselines");

            migrationBuilder.DropColumn(
                name: "FirstSeen",
                table: "FileActivityBaselines");
        }
    }
}
