using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CategoriasListados_CarpetasHojaVida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GrupoEquipoId",
                table: "Equipos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GrupoDocumentoId",
                table: "DocumentosMaestro",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CarpetasHojaVida",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    NombreNormalizado = table.Column<string>(type: "text", nullable: false),
                    Icono = table.Column<string>(type: "text", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarpetasHojaVida", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GruposDocumento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    IconoCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposDocumento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GruposDocumento_IconosCatalogo_IconoCatalogoId",
                        column: x => x.IconoCatalogoId,
                        principalTable: "IconosCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GruposEquipo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    IconoCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposEquipo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GruposEquipo_IconosCatalogo_IconoCatalogoId",
                        column: x => x.IconoCatalogoId,
                        principalTable: "IconosCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ArchivosCarpetaHojaVida",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CarpetaHojaVidaId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: false),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    Ruta = table.Column<string>(type: "text", nullable: false),
                    Tamano = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    FechaCarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosCarpetaHojaVida", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosCarpetaHojaVida_CarpetasHojaVida_CarpetaHojaVidaId",
                        column: x => x.CarpetaHojaVidaId,
                        principalTable: "CarpetasHojaVida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArchivosCarpetaHojaVida_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Equipos_GrupoEquipoId",
                table: "Equipos",
                column: "GrupoEquipoId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosMaestro_GrupoDocumentoId",
                table: "DocumentosMaestro",
                column: "GrupoDocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosCarpetaHojaVida_CarpetaHojaVidaId",
                table: "ArchivosCarpetaHojaVida",
                column: "CarpetaHojaVidaId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosCarpetaHojaVida_ElementoTrazadoId",
                table: "ArchivosCarpetaHojaVida",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_CarpetasHojaVida_NombreNormalizado",
                table: "CarpetasHojaVida",
                column: "NombreNormalizado",
                unique: true,
                filter: "\"Eliminado\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_GruposDocumento_IconoCatalogoId",
                table: "GruposDocumento",
                column: "IconoCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposDocumento_Nombre",
                table: "GruposDocumento",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GruposEquipo_IconoCatalogoId",
                table: "GruposEquipo",
                column: "IconoCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposEquipo_Nombre",
                table: "GruposEquipo",
                column: "Nombre",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentosMaestro_GruposDocumento_GrupoDocumentoId",
                table: "DocumentosMaestro",
                column: "GrupoDocumentoId",
                principalTable: "GruposDocumento",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Equipos_GruposEquipo_GrupoEquipoId",
                table: "Equipos",
                column: "GrupoEquipoId",
                principalTable: "GruposEquipo",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentosMaestro_GruposDocumento_GrupoDocumentoId",
                table: "DocumentosMaestro");

            migrationBuilder.DropForeignKey(
                name: "FK_Equipos_GruposEquipo_GrupoEquipoId",
                table: "Equipos");

            migrationBuilder.DropTable(
                name: "ArchivosCarpetaHojaVida");

            migrationBuilder.DropTable(
                name: "GruposDocumento");

            migrationBuilder.DropTable(
                name: "GruposEquipo");

            migrationBuilder.DropTable(
                name: "CarpetasHojaVida");

            migrationBuilder.DropIndex(
                name: "IX_Equipos_GrupoEquipoId",
                table: "Equipos");

            migrationBuilder.DropIndex(
                name: "IX_DocumentosMaestro_GrupoDocumentoId",
                table: "DocumentosMaestro");

            migrationBuilder.DropColumn(
                name: "GrupoEquipoId",
                table: "Equipos");

            migrationBuilder.DropColumn(
                name: "GrupoDocumentoId",
                table: "DocumentosMaestro");
        }
    }
}
