// SPDX-License-Identifier: Apache-2.0
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LecturaFacil.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeaningVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "meaning_verification",
                table: "adapted_versions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "meaning_verification",
                table: "adapted_versions");
        }
    }
}
