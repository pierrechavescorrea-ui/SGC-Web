using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class IconosYCarpetasElementos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesIcono_IconosCatalogo_IconoCatalogoId",
                table: "AsignacionesIcono");

            migrationBuilder.DropIndex(
                name: "IX_CarpetasHojaVida_NombreNormalizado",
                table: "CarpetasHojaVida");

            migrationBuilder.AddColumn<bool>(
                name: "Eliminado",
                table: "IconosCatalogo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEliminacion",
                table: "IconosCatalogo",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaveElemento",
                table: "CarpetasHojaVida",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "IconoCatalogoId",
                table: "AsignacionesIcono",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ComponenteId",
                table: "ArchivosCarpetaHojaVida",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarpetasHojaVida_ClaveElemento_NombreNormalizado",
                table: "CarpetasHojaVida",
                columns: new[] { "ClaveElemento", "NombreNormalizado" },
                unique: true,
                filter: "\"Eliminado\" = false AND \"ClaveElemento\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CarpetasHojaVida_NombreNormalizado",
                table: "CarpetasHojaVida",
                column: "NombreNormalizado",
                unique: true,
                filter: "\"Eliminado\" = false AND \"ClaveElemento\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosCarpetaHojaVida_ComponenteId",
                table: "ArchivosCarpetaHojaVida",
                column: "ComponenteId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArchivosCarpetaHojaVida_Componentes_ComponenteId",
                table: "ArchivosCarpetaHojaVida",
                column: "ComponenteId",
                principalTable: "Componentes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesIcono_IconosCatalogo_IconoCatalogoId",
                table: "AsignacionesIcono",
                column: "IconoCatalogoId",
                principalTable: "IconosCatalogo",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArchivosCarpetaHojaVida_Componentes_ComponenteId",
                table: "ArchivosCarpetaHojaVida");

            migrationBuilder.DropForeignKey(
                name: "FK_AsignacionesIcono_IconosCatalogo_IconoCatalogoId",
                table: "AsignacionesIcono");

            migrationBuilder.DropIndex(
                name: "IX_CarpetasHojaVida_ClaveElemento_NombreNormalizado",
                table: "CarpetasHojaVida");

            migrationBuilder.DropIndex(
                name: "IX_CarpetasHojaVida_NombreNormalizado",
                table: "CarpetasHojaVida");

            migrationBuilder.DropIndex(
                name: "IX_ArchivosCarpetaHojaVida_ComponenteId",
                table: "ArchivosCarpetaHojaVida");

            migrationBuilder.DropColumn(
                name: "Eliminado",
                table: "IconosCatalogo");

            migrationBuilder.DropColumn(
                name: "FechaEliminacion",
                table: "IconosCatalogo");

            migrationBuilder.DropColumn(
                name: "ClaveElemento",
                table: "CarpetasHojaVida");

            migrationBuilder.DropColumn(
                name: "ComponenteId",
                table: "ArchivosCarpetaHojaVida");

            migrationBuilder.AlterColumn<int>(
                name: "IconoCatalogoId",
                table: "AsignacionesIcono",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarpetasHojaVida_NombreNormalizado",
                table: "CarpetasHojaVida",
                column: "NombreNormalizado",
                unique: true,
                filter: "\"Eliminado\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_AsignacionesIcono_IconosCatalogo_IconoCatalogoId",
                table: "AsignacionesIcono",
                column: "IconoCatalogoId",
                principalTable: "IconosCatalogo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
