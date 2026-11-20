using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class ControlCambiosTiros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CambiosTiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Lote = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    TiroId = table.Column<int>(type: "integer", nullable: true),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    Elemento = table.Column<string>(type: "text", nullable: false),
                    Antes = table.Column<string>(type: "text", nullable: true),
                    Despues = table.Column<string>(type: "text", nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosTiro", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CambiosTiro_Fecha",
                table: "CambiosTiro",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosTiro_Lote",
                table: "CambiosTiro",
                column: "Lote");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CambiosTiro");
        }
    }
}
