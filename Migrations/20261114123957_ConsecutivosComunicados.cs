using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class ConsecutivosComunicados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlantillasConsecutivo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Remite = table.Column<string>(type: "text", nullable: false),
                    Destinatario = table.Column<string>(type: "text", nullable: false),
                    Proyecto = table.Column<string>(type: "text", nullable: false),
                    CodigoProyecto = table.Column<string>(type: "text", nullable: false),
                    ReiniciaCadaAnio = table.Column<bool>(type: "boolean", nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillasConsecutivo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReservasConsecutivo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlantillaConsecutivoId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Anio = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    SolicitadoPor = table.Column<string>(type: "text", nullable: false),
                    Origen = table.Column<string>(type: "text", nullable: false),
                    ChatIdTelegram = table.Column<long>(type: "bigint", nullable: true),
                    FechaSolicitud = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FechaEnvioPrevista = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Asunto = table.Column<string>(type: "text", nullable: false),
                    DirigidoA = table.Column<string>(type: "text", nullable: true),
                    Tipo = table.Column<string>(type: "text", nullable: true),
                    RespondeA = table.Column<string>(type: "text", nullable: true),
                    RequiereRespuesta = table.Column<bool>(type: "boolean", nullable: true),
                    ResponsableRespuesta = table.Column<string>(type: "text", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    FechaEnvio = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ComunicadoId = table.Column<int>(type: "integer", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "text", nullable: true),
                    CerradoPor = table.Column<string>(type: "text", nullable: true),
                    FechaCierre = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasConsecutivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservasConsecutivo_Comunicados_ComunicadoId",
                        column: x => x.ComunicadoId,
                        principalTable: "Comunicados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReservasConsecutivo_PlantillasConsecutivo_PlantillaConsecut~",
                        column: x => x.PlantillaConsecutivoId,
                        principalTable: "PlantillasConsecutivo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasConsecutivo_Codigo",
                table: "ReservasConsecutivo",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservasConsecutivo_ComunicadoId",
                table: "ReservasConsecutivo",
                column: "ComunicadoId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasConsecutivo_Estado",
                table: "ReservasConsecutivo",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasConsecutivo_PlantillaConsecutivoId_Anio_Numero",
                table: "ReservasConsecutivo",
                columns: new[] { "PlantillaConsecutivoId", "Anio", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservasConsecutivo");

            migrationBuilder.DropTable(
                name: "PlantillasConsecutivo");
        }
    }
}
