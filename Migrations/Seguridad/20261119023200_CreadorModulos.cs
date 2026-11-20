using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations.Seguridad
{
    /// <inheritdoc />
    public partial class CreadorModulos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModulosCreados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Clave = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Icono = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Grupo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Ambito = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DefinicionJson = table.Column<string>(type: "text", nullable: true),
                    BorradorJson = table.Column<string>(type: "text", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    CreadoPor = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PublicadoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModulosCreados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VersionesModuloCreado",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModuloCreadoId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Nota = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PublicadoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PublicadoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionesModuloCreado", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersionesModuloCreado_ModulosCreados_ModuloCreadoId",
                        column: x => x.ModuloCreadoId,
                        principalTable: "ModulosCreados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModulosCreados_Clave",
                table: "ModulosCreados",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VersionesModuloCreado_ModuloCreadoId_Version",
                table: "VersionesModuloCreado",
                columns: new[] { "ModuloCreadoId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VersionesModuloCreado");

            migrationBuilder.DropTable(
                name: "ModulosCreados");
        }
    }
}
