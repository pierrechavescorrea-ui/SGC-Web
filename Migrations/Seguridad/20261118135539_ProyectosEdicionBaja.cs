using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigWeb.Migrations.Seguridad
{
    /// <inheritdoc />
    public partial class ProyectosEdicionBaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoConexion",
                table: "Proyectos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstadoAntesEliminacion",
                table: "Proyectos",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodigoConexion",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "EstadoAntesEliminacion",
                table: "Proyectos");
        }
    }
}
