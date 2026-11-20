using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPartesInteresadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeguimientoParteInteresadaId",
                table: "PlanesAccion",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgramacionesSeguimientoParte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParteClave = table.Column<Guid>(type: "uuid", nullable: false),
                    Cada = table.Column<int>(type: "integer", nullable: true),
                    Unidad = table.Column<string>(type: "text", nullable: false),
                    Desde = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Responsable = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramacionesSeguimientoParte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VersionesPartesInteresadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: true),
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
                    VersionBaseId = table.Column<int>(type: "integer", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionesPartesInteresadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersionesPartesInteresadas_VersionesPartesInteresadas_Versi~",
                        column: x => x.VersionBaseId,
                        principalTable: "VersionesPartesInteresadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PartesInteresadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VersionPartesInteresadasId = table.Column<int>(type: "integer", nullable: false),
                    Clave = table.Column<Guid>(type: "uuid", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Procesos = table.Column<string>(type: "text", nullable: true),
                    Necesidades = table.Column<string>(type: "text", nullable: true),
                    Expectativas = table.Column<string>(type: "text", nullable: true),
                    AfectaSgc = table.Column<string>(type: "text", nullable: true),
                    Impacto = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Influencia = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Clasificacion = table.Column<string>(type: "text", nullable: true),
                    Estrategias = table.Column<string>(type: "text", nullable: true),
                    Medicion = table.Column<string>(type: "text", nullable: true),
                    Periodicidad = table.Column<string>(type: "text", nullable: true),
                    Meta = table.Column<string>(type: "text", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    Supuesto = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartesInteresadas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartesInteresadas_VersionesPartesInteresadas_VersionPartesI~",
                        column: x => x.VersionPartesInteresadasId,
                        principalTable: "VersionesPartesInteresadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeguimientosParteInteresada",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParteClave = table.Column<Guid>(type: "uuid", nullable: false),
                    ParteInteresadaId = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Responsable = table.Column<string>(type: "text", nullable: true),
                    Resultado = table.Column<string>(type: "text", nullable: true),
                    Cumplimiento = table.Column<int>(type: "integer", nullable: false),
                    Impacto = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Influencia = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Observacion = table.Column<string>(type: "text", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeguimientosParteInteresada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeguimientosParteInteresada_PartesInteresadas_ParteInteresa~",
                        column: x => x.ParteInteresadaId,
                        principalTable: "PartesInteresadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanesAccion_SeguimientoParteInteresadaId",
                table: "PlanesAccion",
                column: "SeguimientoParteInteresadaId");

            migrationBuilder.CreateIndex(
                name: "IX_PartesInteresadas_Clave",
                table: "PartesInteresadas",
                column: "Clave");

            migrationBuilder.CreateIndex(
                name: "IX_PartesInteresadas_VersionPartesInteresadasId",
                table: "PartesInteresadas",
                column: "VersionPartesInteresadasId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramacionesSeguimientoParte_ParteClave",
                table: "ProgramacionesSeguimientoParte",
                column: "ParteClave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeguimientosParteInteresada_ParteClave",
                table: "SeguimientosParteInteresada",
                column: "ParteClave");

            migrationBuilder.CreateIndex(
                name: "IX_SeguimientosParteInteresada_ParteInteresadaId",
                table: "SeguimientosParteInteresada",
                column: "ParteInteresadaId");

            migrationBuilder.CreateIndex(
                name: "IX_VersionesPartesInteresadas_VersionBaseId",
                table: "VersionesPartesInteresadas",
                column: "VersionBaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanesAccion_SeguimientosParteInteresada_SeguimientoParteIn~",
                table: "PlanesAccion",
                column: "SeguimientoParteInteresadaId",
                principalTable: "SeguimientosParteInteresada",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanesAccion_SeguimientosParteInteresada_SeguimientoParteIn~",
                table: "PlanesAccion");

            migrationBuilder.DropTable(
                name: "ProgramacionesSeguimientoParte");

            migrationBuilder.DropTable(
                name: "SeguimientosParteInteresada");

            migrationBuilder.DropTable(
                name: "PartesInteresadas");

            migrationBuilder.DropTable(
                name: "VersionesPartesInteresadas");

            migrationBuilder.DropIndex(
                name: "IX_PlanesAccion_SeguimientoParteInteresadaId",
                table: "PlanesAccion");

            migrationBuilder.DropColumn(
                name: "SeguimientoParteInteresadaId",
                table: "PlanesAccion");
        }
    }
}
