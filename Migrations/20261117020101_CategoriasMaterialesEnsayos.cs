using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CategoriasMaterialesEnsayos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GrupoResultadoEnsayoId",
                table: "ResultadosEnsayo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GrupoMaterialId",
                table: "Materiales",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GruposMaterial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    IconoCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Proyecto = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposMaterial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GruposMaterial_IconosCatalogo_IconoCatalogoId",
                        column: x => x.IconoCatalogoId,
                        principalTable: "IconosCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GruposResultadoEnsayo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoEnsayoId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    IconoCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Proyecto = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GruposResultadoEnsayo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GruposResultadoEnsayo_IconosCatalogo_IconoCatalogoId",
                        column: x => x.IconoCatalogoId,
                        principalTable: "IconosCatalogo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GruposResultadoEnsayo_TiposEnsayo_TipoEnsayoId",
                        column: x => x.TipoEnsayoId,
                        principalTable: "TiposEnsayo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosEnsayo_GrupoResultadoEnsayoId",
                table: "ResultadosEnsayo",
                column: "GrupoResultadoEnsayoId");

            migrationBuilder.CreateIndex(
                name: "IX_Materiales_GrupoMaterialId",
                table: "Materiales",
                column: "GrupoMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposMaterial_IconoCatalogoId",
                table: "GruposMaterial",
                column: "IconoCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposMaterial_Nombre",
                table: "GruposMaterial",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GruposResultadoEnsayo_IconoCatalogoId",
                table: "GruposResultadoEnsayo",
                column: "IconoCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_GruposResultadoEnsayo_TipoEnsayoId_Nombre",
                table: "GruposResultadoEnsayo",
                columns: new[] { "TipoEnsayoId", "Nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Materiales_GruposMaterial_GrupoMaterialId",
                table: "Materiales",
                column: "GrupoMaterialId",
                principalTable: "GruposMaterial",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ResultadosEnsayo_GruposResultadoEnsayo_GrupoResultadoEnsayo~",
                table: "ResultadosEnsayo",
                column: "GrupoResultadoEnsayoId",
                principalTable: "GruposResultadoEnsayo",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materiales_GruposMaterial_GrupoMaterialId",
                table: "Materiales");

            migrationBuilder.DropForeignKey(
                name: "FK_ResultadosEnsayo_GruposResultadoEnsayo_GrupoResultadoEnsayo~",
                table: "ResultadosEnsayo");

            migrationBuilder.DropTable(
                name: "GruposMaterial");

            migrationBuilder.DropTable(
                name: "GruposResultadoEnsayo");

            migrationBuilder.DropIndex(
                name: "IX_ResultadosEnsayo_GrupoResultadoEnsayoId",
                table: "ResultadosEnsayo");

            migrationBuilder.DropIndex(
                name: "IX_Materiales_GrupoMaterialId",
                table: "Materiales");

            migrationBuilder.DropColumn(
                name: "GrupoResultadoEnsayoId",
                table: "ResultadosEnsayo");

            migrationBuilder.DropColumn(
                name: "GrupoMaterialId",
                table: "Materiales");
        }
    }
}
