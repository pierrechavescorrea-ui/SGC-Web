using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class RevisionesTablaTorres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RevisionesTablaTorres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    NombreArchivo = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: true),
                    FechaCarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Torres = table.Column<int>(type: "integer", nullable: false),
                    Cambios = table.Column<int>(type: "integer", nullable: false),
                    FechaDeshecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoDeshacer = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionesTablaTorres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CambiosTablaTorres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RevisionTablaTorresId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: false),
                    CodigoTorre = table.Column<string>(type: "text", nullable: false),
                    ComponenteId = table.Column<int>(type: "integer", nullable: true),
                    Ambito = table.Column<string>(type: "text", nullable: false),
                    Clave = table.Column<string>(type: "text", nullable: false),
                    ValorAnterior = table.Column<string>(type: "text", nullable: true),
                    ValorNuevo = table.Column<string>(type: "text", nullable: true),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Observacion = table.Column<string>(type: "text", nullable: true),
                    CampoNuevoId = table.Column<int>(type: "integer", nullable: true),
                    CampoAnteriorId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosTablaTorres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CambiosTablaTorres_RevisionesTablaTorres_RevisionTablaTorre~",
                        column: x => x.RevisionTablaTorresId,
                        principalTable: "RevisionesTablaTorres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CambiosTablaTorres_ElementoTrazadoId",
                table: "CambiosTablaTorres",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosTablaTorres_RevisionTablaTorresId",
                table: "CambiosTablaTorres",
                column: "RevisionTablaTorresId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionesTablaTorres_Codigo",
                table: "RevisionesTablaTorres",
                column: "Codigo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CambiosTablaTorres");

            migrationBuilder.DropTable(
                name: "RevisionesTablaTorres");
        }
    }
}
