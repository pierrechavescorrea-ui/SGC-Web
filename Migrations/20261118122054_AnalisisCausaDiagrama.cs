using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AnalisisCausaDiagrama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalisisCausaDiagrama",
                table: "PlanesAccion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalisisCausaSvg",
                table: "PlanesAccion",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalisisCausaDiagrama",
                table: "PlanesAccion");

            migrationBuilder.DropColumn(
                name: "AnalisisCausaSvg",
                table: "PlanesAccion");
        }
    }
}
