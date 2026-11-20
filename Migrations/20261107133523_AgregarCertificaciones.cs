using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCertificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Certificaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Norma = table.Column<string>(type: "text", nullable: false),
                    Sistema = table.Column<string>(type: "text", nullable: true),
                    Alcance = table.Column<string>(type: "text", nullable: true),
                    Organismo = table.Column<string>(type: "text", nullable: true),
                    Numero = table.Column<string>(type: "text", nullable: true),
                    Emision = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Vencimiento = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    DocumentoMaestroId = table.Column<int>(type: "integer", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Certificaciones_DocumentosMaestro_DocumentoMaestroId",
                        column: x => x.DocumentoMaestroId,
                        principalTable: "DocumentosMaestro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Certificaciones_DocumentoMaestroId",
                table: "Certificaciones",
                column: "DocumentoMaestroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Certificaciones");
        }
    }
}
