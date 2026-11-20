using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class VistasYCamposSegunOpcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CampoPadreId",
                table: "CamposTabla",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpcionPadre",
                table: "CamposTabla",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VistasRegistro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Modulo = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false),
                    Predeterminada = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VistasRegistro", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VistasRegistro_Modulo",
                table: "VistasRegistro",
                column: "Modulo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VistasRegistro");

            migrationBuilder.DropColumn(
                name: "CampoPadreId",
                table: "CamposTabla");

            migrationBuilder.DropColumn(
                name: "OpcionPadre",
                table: "CamposTabla");
        }
    }
}
