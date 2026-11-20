using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOrganigrama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VersionesOrganigrama",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Alcance = table.Column<string>(type: "text", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    VigenteDesde = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    VigenteHasta = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MotivoCambio = table.Column<string>(type: "text", nullable: true),
                    Elaboro = table.Column<string>(type: "text", nullable: true),
                    Reviso = table.Column<string>(type: "text", nullable: true),
                    Aprobo = table.Column<string>(type: "text", nullable: true),
                    FechaAprobacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Origen = table.Column<string>(type: "text", nullable: true),
                    Notas = table.Column<string>(type: "text", nullable: true),
                    VersionBaseId = table.Column<int>(type: "integer", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionesOrganigrama", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersionesOrganigrama_VersionesOrganigrama_VersionBaseId",
                        column: x => x.VersionBaseId,
                        principalTable: "VersionesOrganigrama",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CargosOrganigrama",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VersionOrganigramaId = table.Column<int>(type: "integer", nullable: false),
                    Clave = table.Column<Guid>(type: "uuid", nullable: false),
                    JefeId = table.Column<int>(type: "integer", nullable: true),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Ocupante = table.Column<string>(type: "text", nullable: true),
                    Area = table.Column<string>(type: "text", nullable: true),
                    Plazas = table.Column<int>(type: "integer", nullable: true),
                    Staff = table.Column<bool>(type: "boolean", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Notas = table.Column<string>(type: "text", nullable: true),
                    Supuesto = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargosOrganigrama", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CargosOrganigrama_CargosOrganigrama_JefeId",
                        column: x => x.JefeId,
                        principalTable: "CargosOrganigrama",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CargosOrganigrama_VersionesOrganigrama_VersionOrganigramaId",
                        column: x => x.VersionOrganigramaId,
                        principalTable: "VersionesOrganigrama",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CargosOrganigrama_Clave",
                table: "CargosOrganigrama",
                column: "Clave");

            migrationBuilder.CreateIndex(
                name: "IX_CargosOrganigrama_JefeId",
                table: "CargosOrganigrama",
                column: "JefeId");

            migrationBuilder.CreateIndex(
                name: "IX_CargosOrganigrama_VersionOrganigramaId",
                table: "CargosOrganigrama",
                column: "VersionOrganigramaId");

            migrationBuilder.CreateIndex(
                name: "IX_VersionesOrganigrama_VersionBaseId",
                table: "VersionesOrganigrama",
                column: "VersionBaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CargosOrganigrama");

            migrationBuilder.DropTable(
                name: "VersionesOrganigrama");
        }
    }
}
