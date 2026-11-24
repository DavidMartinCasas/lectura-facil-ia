// SPDX-License-Identifier: Apache-2.0
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LecturaFacil.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingPart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "processing_part",
                table: "documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "processing_parts",
                table: "documents",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "processing_part",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "processing_parts",
                table: "documents");
        }
    }
}
