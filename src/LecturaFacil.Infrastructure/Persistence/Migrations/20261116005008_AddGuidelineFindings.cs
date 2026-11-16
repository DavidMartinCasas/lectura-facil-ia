// SPDX-License-Identifier: Apache-2.0
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LecturaFacil.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidelineFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guidelines",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    default_severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guidelines", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "findings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    guideline_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fragment = table.Column<string>(type: "text", nullable: false),
                    start_position = table.Column<int>(type: "integer", nullable: false),
                    end_position = table.Column<int>(type: "integer", nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    suggestion = table.Column<string>(type: "text", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_findings", x => x.id);
                    table.ForeignKey(
                        name: "FK_findings_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_findings_guidelines_guideline_code",
                        column: x => x.guideline_code,
                        principalTable: "guidelines",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "guidelines",
                columns: new[] { "code", "default_severity", "description" },
                values: new object[,]
                {
                    { "FECHAS_COMPLETAS", "Low", "Fechas completas" },
                    { "FRASES_CORTAS", "Medium", "Frases cortas" },
                    { "NUMEROS_EN_CIFRAS", "Low", "Números escritos con cifras" },
                    { "PALABRAS_DIFICILES", "Medium", "Palabras difíciles explicadas" },
                    { "SIN_EXPRESIONES_JURIDICAS", "High", "Sin expresiones jurídicas complejas" },
                    { "SIN_NEGACIONES_DOBLES", "High", "Sin negaciones dobles" },
                    { "SIN_NUMEROS_ROMANOS", "Medium", "Sin números romanos" },
                    { "SIN_PORCENTAJES", "Medium", "Sin porcentajes" },
                    { "SIN_SIGLAS", "High", "Sin siglas sin explicar" },
                    { "SIN_VOZ_PASIVA", "Medium", "Sin voz pasiva" },
                    { "UNA_IDEA_POR_FRASE", "Low", "Una idea por frase" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_findings_document_id",
                table: "findings",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_findings_guideline_code",
                table: "findings",
                column: "guideline_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "findings");

            migrationBuilder.DropTable(
                name: "guidelines");
        }
    }
}
