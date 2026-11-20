using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CreadorModulos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrosModulo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Modulo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Consecutivo = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Estado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PasosNoAplica = table.Column<string>(type: "text", nullable: true),
                    VersionDefinicion = table.Column<int>(type: "integer", nullable: false),
                    CreadoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ModificadoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Proyecto = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosModulo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchivosRegistroModulo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RegistroModuloId = table.Column<int>(type: "integer", nullable: false),
                    Campo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Ruta = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Tamano = table.Column<long>(type: "bigint", nullable: false),
                    SubidoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SubidoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosRegistroModulo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosRegistroModulo_RegistrosModulo_RegistroModuloId",
                        column: x => x.RegistroModuloId,
                        principalTable: "RegistrosModulo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VinculosRegistroModulo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RegistroModuloId = table.Column<int>(type: "integer", nullable: false),
                    Campo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Fuente = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ElementoId = table.Column<int>(type: "integer", nullable: false),
                    Etiqueta = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VinculosRegistroModulo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VinculosRegistroModulo_RegistrosModulo_RegistroModuloId",
                        column: x => x.RegistroModuloId,
                        principalTable: "RegistrosModulo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosRegistroModulo_RegistroModuloId_Campo",
                table: "ArchivosRegistroModulo",
                columns: new[] { "RegistroModuloId", "Campo" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosModulo_Modulo_Consecutivo",
                table: "RegistrosModulo",
                columns: new[] { "Modulo", "Consecutivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosModulo_Modulo_Eliminado",
                table: "RegistrosModulo",
                columns: new[] { "Modulo", "Eliminado" });

            migrationBuilder.CreateIndex(
                name: "IX_VinculosRegistroModulo_Fuente_ElementoId",
                table: "VinculosRegistroModulo",
                columns: new[] { "Fuente", "ElementoId" });

            migrationBuilder.CreateIndex(
                name: "IX_VinculosRegistroModulo_RegistroModuloId_Campo",
                table: "VinculosRegistroModulo",
                columns: new[] { "RegistroModuloId", "Campo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosRegistroModulo");

            migrationBuilder.DropTable(
                name: "VinculosRegistroModulo");

            migrationBuilder.DropTable(
                name: "RegistrosModulo");
        }
    }
}
