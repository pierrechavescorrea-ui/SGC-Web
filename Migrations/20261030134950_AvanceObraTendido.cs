using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AvanceObraTendido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrosAvanceTiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InformeDiarioId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TiroId = table.Column<int>(type: "integer", nullable: false),
                    ActividadObraId = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: true),
                    AvanceAcumulado = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    NoAplica = table.Column<bool>(type: "boolean", nullable: false),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosAvanceTiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosAvanceTiro_ActividadesObra_ActividadObraId",
                        column: x => x.ActividadObraId,
                        principalTable: "ActividadesObra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosAvanceTiro_InformesDiarios_InformeDiarioId",
                        column: x => x.InformeDiarioId,
                        principalTable: "InformesDiarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistrosAvanceTiro_Tiros_TiroId",
                        column: x => x.TiroId,
                        principalTable: "Tiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAvanceTiro_ActividadObraId",
                table: "RegistrosAvanceTiro",
                column: "ActividadObraId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAvanceTiro_InformeDiarioId",
                table: "RegistrosAvanceTiro",
                column: "InformeDiarioId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAvanceTiro_TiroId_Fecha",
                table: "RegistrosAvanceTiro",
                columns: new[] { "TiroId", "Fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosAvanceTiro");
        }
    }
}
