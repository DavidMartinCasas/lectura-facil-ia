// SPDX-License-Identifier: Apache-2.0
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LecturaFacil.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddValidationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "hard_glossary",
                table: "validations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "hard_sections",
                table: "validations",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<int>(
                name: "participants",
                table: "validations",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hard_glossary",
                table: "validations");

            migrationBuilder.DropColumn(
                name: "hard_sections",
                table: "validations");

            migrationBuilder.DropColumn(
                name: "participants",
                table: "validations");
        }
    }
}
