using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPlanAuditoria22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuditoriaId",
                table: "PlanesAccion",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanesAccion_AuditoriaId",
                table: "PlanesAccion",
                column: "AuditoriaId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanesAccion_Auditorias_AuditoriaId",
                table: "PlanesAccion",
                column: "AuditoriaId",
                principalTable: "Auditorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlanesAccion_Auditorias_AuditoriaId",
                table: "PlanesAccion");

            migrationBuilder.DropIndex(
                name: "IX_PlanesAccion_AuditoriaId",
                table: "PlanesAccion");

            migrationBuilder.DropColumn(
                name: "AuditoriaId",
                table: "PlanesAccion");
        }
    }
}
