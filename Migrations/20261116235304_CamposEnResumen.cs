using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CamposEnResumen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnResumen",
                table: "CamposTabla",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnResumen",
                table: "CamposEquipo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnResumen",
                table: "CamposEnsayo",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnResumen",
                table: "CamposTabla");

            migrationBuilder.DropColumn(
                name: "EnResumen",
                table: "CamposEquipo");

            migrationBuilder.DropColumn(
                name: "EnResumen",
                table: "CamposEnsayo");
        }
    }
}
