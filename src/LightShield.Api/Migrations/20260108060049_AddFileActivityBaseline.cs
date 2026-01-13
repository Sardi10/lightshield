using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightShield.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFileActivityBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileActivityBaselines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Hostname = table.Column<string>(type: "TEXT", nullable: false),
                    CreateAvg = table.Column<double>(type: "REAL", nullable: false),
                    ModifyAvg = table.Column<double>(type: "REAL", nullable: false),
                    DeleteAvg = table.Column<double>(type: "REAL", nullable: false),
                    RenameAvg = table.Column<double>(type: "REAL", nullable: false),
                    CreateStd = table.Column<double>(type: "REAL", nullable: false),
                    ModifyStd = table.Column<double>(type: "REAL", nullable: false),
                    DeleteStd = table.Column<double>(type: "REAL", nullable: false),
                    RenameStd = table.Column<double>(type: "REAL", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileActivityBaselines", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileActivityBaselines");
        }
    }
}
