using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class PartesInteresadasActivas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Inactiva",
                table: "ProgramacionesSeguimientoParte",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "InactivaDesde",
                table: "ProgramacionesSeguimientoParte",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Inactiva",
                table: "ProgramacionesSeguimientoParte");

            migrationBuilder.DropColumn(
                name: "InactivaDesde",
                table: "ProgramacionesSeguimientoParte");
        }
    }
}
