// SPDX-License-Identifier: Apache-2.0
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LecturaFacil.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidelineReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "guideline_report",
                table: "adapted_versions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.InsertData(
                table: "guidelines",
                columns: new[] { "code", "default_severity", "description" },
                values: new object[] { "TITULOS_CLAROS", "Low", "Títulos claros" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "guidelines",
                keyColumn: "code",
                keyValue: "TITULOS_CLAROS");

            migrationBuilder.DropColumn(
                name: "guideline_report",
                table: "adapted_versions");
        }
    }
}
