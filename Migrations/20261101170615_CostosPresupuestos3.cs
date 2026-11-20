using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CostosPresupuestos3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActividadesPrograma_ProgramaTrabajoId",
                table: "ActividadesPrograma");

            migrationBuilder.AddColumn<string>(
                name: "DiasLaborables",
                table: "ProgramasTrabajo",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "1111111");

            migrationBuilder.AddColumn<string>(
                name: "Festivos",
                table: "ProgramasTrabajo",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ItemPagoId",
                table: "ActividadesPrograma",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ActividadObraId",
                table: "ActividadesPrograma",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AvanceProject",
                table: "ActividadesPrograma",
                type: "numeric(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Duracion",
                table: "ActividadesPrograma",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EsResumen",
                table: "ActividadesPrograma",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Nivel",
                table: "ActividadesPrograma",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Nombre",
                table: "ActividadesPrograma",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Orden",
                table: "ActividadesPrograma",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Uid",
                table: "ActividadesPrograma",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorPlaneado",
                table: "ActividadesPrograma",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Wbs",
                table: "ActividadesPrograma",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModelosIfcCargados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Familia = table.Column<string>(type: "text", nullable: true),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    Cambios = table.Column<string>(type: "text", nullable: true),
                    ArchivoIfc = table.Column<string>(type: "text", nullable: false),
                    NombreOriginalIfc = table.Column<string>(type: "text", nullable: false),
                    ArchivoJson = table.Column<string>(type: "text", nullable: true),
                    ArchivoMiniatura = table.Column<string>(type: "text", nullable: true),
                    ArchivoInforme = table.Column<string>(type: "text", nullable: true),
                    NombreOriginalInforme = table.Column<string>(type: "text", nullable: true),
                    Validaciones = table.Column<int>(type: "integer", nullable: false),
                    Supuestos = table.Column<int>(type: "integer", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelosIfcCargados", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesPrograma_ActividadObraId",
                table: "ActividadesPrograma",
                column: "ActividadObraId");

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesPrograma_ProgramaTrabajoId_Uid",
                table: "ActividadesPrograma",
                columns: new[] { "ProgramaTrabajoId", "Uid" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelosIfcCargados_Codigo_Version",
                table: "ModelosIfcCargados",
                columns: new[] { "Codigo", "Version" });

            migrationBuilder.AddForeignKey(
                name: "FK_ActividadesPrograma_ActividadesObra_ActividadObraId",
                table: "ActividadesPrograma",
                column: "ActividadObraId",
                principalTable: "ActividadesObra",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActividadesPrograma_ActividadesObra_ActividadObraId",
                table: "ActividadesPrograma");

            migrationBuilder.DropTable(
                name: "ModelosIfcCargados");

            migrationBuilder.DropIndex(
                name: "IX_ActividadesPrograma_ActividadObraId",
                table: "ActividadesPrograma");

            migrationBuilder.DropIndex(
                name: "IX_ActividadesPrograma_ProgramaTrabajoId_Uid",
                table: "ActividadesPrograma");

            migrationBuilder.DropColumn(
                name: "DiasLaborables",
                table: "ProgramasTrabajo");

            migrationBuilder.DropColumn(
                name: "Festivos",
                table: "ProgramasTrabajo");

            migrationBuilder.DropColumn(
                name: "ActividadObraId",
                table: "ActividadesPrograma");

            migrationBuilder.DropColumn(
                name: "AvanceProject",
                table: "ActividadesPrograma");

            migrationBuilder.DropColumn(
                name: "Duracion",
                table: "ActividadesPrograma");

            migrationBuilder.DropColumn(
                name: "EsResumen",
                table: "ActividadesPrograma");

            migrationBuilder.DropColumn(
                name: "Nivel",
                table: "ActividadesPrograma");

            migrationBuilder.DropColumn(
                name: "Nombre",
                table: "ActividadesPrograma");

            migrationBuilder.DropColumn(
                name: "Orden",
                table: "ActividadesPrograma");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "ActividadesPrograma");

            migrationBuilder.DropColumn(
                name: "ValorPlaneado",
                table: "ActividadesPrograma");

            migrationBuilder.DropColumn(
                name: "Wbs",
                table: "ActividadesPrograma");

            migrationBuilder.AlterColumn<int>(
                name: "ItemPagoId",
                table: "ActividadesPrograma",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesPrograma_ProgramaTrabajoId",
                table: "ActividadesPrograma",
                column: "ProgramaTrabajoId");
        }
    }
}
