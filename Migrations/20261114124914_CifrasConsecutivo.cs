using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CifrasConsecutivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnioNumeroInicial",
                table: "PlantillasConsecutivo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CifrasNumero",
                table: "PlantillasConsecutivo",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<int>(
                name: "NumeroInicial",
                table: "PlantillasConsecutivo",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnioNumeroInicial",
                table: "PlantillasConsecutivo");

            migrationBuilder.DropColumn(
                name: "CifrasNumero",
                table: "PlantillasConsecutivo");

            migrationBuilder.DropColumn(
                name: "NumeroInicial",
                table: "PlantillasConsecutivo");
        }
    }
}
