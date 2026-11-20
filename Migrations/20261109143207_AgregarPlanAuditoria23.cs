using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPlanAuditoria23 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CriterioEficacia",
                table: "PlanesAccion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaProgramadaEficacia",
                table: "PlanesAccion",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriterioEficacia",
                table: "PlanesAccion");

            migrationBuilder.DropColumn(
                name: "FechaProgramadaEficacia",
                table: "PlanesAccion");
        }
    }
}
