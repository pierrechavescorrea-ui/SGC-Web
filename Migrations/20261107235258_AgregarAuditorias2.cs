using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAuditorias2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Eficaz",
                table: "PlanesAccion",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenciaEficacia",
                table: "PlanesAccion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaVerificacionEficacia",
                table: "PlanesAccion",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificoEficacia",
                table: "PlanesAccion",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgramasAuditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Anio = table.Column<int>(type: "integer", nullable: false),
                    Proyecto = table.Column<string>(type: "text", nullable: true),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Normas = table.Column<string>(type: "text", nullable: false),
                    Objetivos = table.Column<string>(type: "text", nullable: true),
                    Responsable = table.Column<string>(type: "text", nullable: true),
                    Recursos = table.Column<string>(type: "text", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Aprobo = table.Column<string>(type: "text", nullable: true),
                    FechaAprobacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramasAuditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Auditorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProgramaAuditoriaId = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Modalidad = table.Column<string>(type: "text", nullable: false),
                    Mes = table.Column<int>(type: "integer", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FechaFin = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Proyecto = table.Column<string>(type: "text", nullable: true),
                    Procesos = table.Column<string>(type: "text", nullable: false),
                    Normas = table.Column<string>(type: "text", nullable: false),
                    Objetivo = table.Column<string>(type: "text", nullable: true),
                    Alcance = table.Column<string>(type: "text", nullable: true),
                    Criterios = table.Column<string>(type: "text", nullable: true),
                    AuditorLider = table.Column<string>(type: "text", nullable: true),
                    Auditores = table.Column<string>(type: "text", nullable: false),
                    ExpertoTecnico = table.Column<string>(type: "text", nullable: true),
                    Observadores = table.Column<string>(type: "text", nullable: true),
                    Auditados = table.Column<string>(type: "text", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Conclusiones = table.Column<string>(type: "text", nullable: true),
                    Fortalezas = table.Column<string>(type: "text", nullable: true),
                    FechaInforme = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auditorias_ProgramasAuditoria_ProgramaAuditoriaId",
                        column: x => x.ProgramaAuditoriaId,
                        principalTable: "ProgramasAuditoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CambiosAuditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuditoriaId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Campo = table.Column<string>(type: "text", nullable: false),
                    Anterior = table.Column<string>(type: "text", nullable: true),
                    Nuevo = table.Column<string>(type: "text", nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CambiosAuditoria_Auditorias_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HallazgosAuditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuditoriaId = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Grado = table.Column<string>(type: "text", nullable: true),
                    Normas = table.Column<string>(type: "text", nullable: false),
                    Numerales = table.Column<string>(type: "text", nullable: true),
                    Documento = table.Column<string>(type: "text", nullable: true),
                    Requisito = table.Column<string>(type: "text", nullable: true),
                    Evidencia = table.Column<string>(type: "text", nullable: true),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Procesos = table.Column<string>(type: "text", nullable: false),
                    Auditor = table.Column<string>(type: "text", nullable: true),
                    Aceptacion = table.Column<int>(type: "integer", nullable: false),
                    NotaAceptacion = table.Column<string>(type: "text", nullable: true),
                    FechaCierre = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CerradoPor = table.Column<string>(type: "text", nullable: true),
                    ObservacionCierre = table.Column<string>(type: "text", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallazgosAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HallazgosAuditoria_Auditorias_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LugaresAuditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuditoriaId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    ComponenteId = table.Column<int>(type: "integer", nullable: true),
                    ItemCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    TiroId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LugaresAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LugaresAuditoria_Auditorias_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresAuditoria_Catalogos_ItemCatalogoId",
                        column: x => x.ItemCatalogoId,
                        principalTable: "Catalogos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresAuditoria_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresAuditoria_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresAuditoria_Tiros_TiroId",
                        column: x => x.TiroId,
                        principalTable: "Tiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchivosHallazgoAuditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HallazgoAuditoriaId = table.Column<int>(type: "integer", nullable: false),
                    NombreOriginal = table.Column<string>(type: "text", nullable: false),
                    RutaArchivo = table.Column<string>(type: "text", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosHallazgoAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivosHallazgoAuditoria_HallazgosAuditoria_HallazgoAudito~",
                        column: x => x.HallazgoAuditoriaId,
                        principalTable: "HallazgosAuditoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HallazgosPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HallazgoAuditoriaId = table.Column<int>(type: "integer", nullable: false),
                    PlanAccionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallazgosPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HallazgosPlan_HallazgosAuditoria_HallazgoAuditoriaId",
                        column: x => x.HallazgoAuditoriaId,
                        principalTable: "HallazgosAuditoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HallazgosPlan_PlanesAccion_PlanAccionId",
                        column: x => x.PlanAccionId,
                        principalTable: "PlanesAccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LugaresHallazgoAuditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HallazgoAuditoriaId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    ComponenteId = table.Column<int>(type: "integer", nullable: true),
                    ItemCatalogoId = table.Column<int>(type: "integer", nullable: true),
                    TiroId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LugaresHallazgoAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LugaresHallazgoAuditoria_Catalogos_ItemCatalogoId",
                        column: x => x.ItemCatalogoId,
                        principalTable: "Catalogos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresHallazgoAuditoria_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresHallazgoAuditoria_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresHallazgoAuditoria_HallazgosAuditoria_HallazgoAuditor~",
                        column: x => x.HallazgoAuditoriaId,
                        principalTable: "HallazgosAuditoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LugaresHallazgoAuditoria_Tiros_TiroId",
                        column: x => x.TiroId,
                        principalTable: "Tiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivosHallazgoAuditoria_HallazgoAuditoriaId",
                table: "ArchivosHallazgoAuditoria",
                column: "HallazgoAuditoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_Codigo",
                table: "Auditorias",
                column: "Codigo");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_ProgramaAuditoriaId",
                table: "Auditorias",
                column: "ProgramaAuditoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosAuditoria_AuditoriaId",
                table: "CambiosAuditoria",
                column: "AuditoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_HallazgosAuditoria_AuditoriaId",
                table: "HallazgosAuditoria",
                column: "AuditoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_HallazgosPlan_HallazgoAuditoriaId_PlanAccionId",
                table: "HallazgosPlan",
                columns: new[] { "HallazgoAuditoriaId", "PlanAccionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HallazgosPlan_PlanAccionId",
                table: "HallazgosPlan",
                column: "PlanAccionId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresAuditoria_AuditoriaId",
                table: "LugaresAuditoria",
                column: "AuditoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresAuditoria_ComponenteId",
                table: "LugaresAuditoria",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresAuditoria_ElementoTrazadoId",
                table: "LugaresAuditoria",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresAuditoria_ItemCatalogoId",
                table: "LugaresAuditoria",
                column: "ItemCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresAuditoria_TiroId",
                table: "LugaresAuditoria",
                column: "TiroId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresHallazgoAuditoria_ComponenteId",
                table: "LugaresHallazgoAuditoria",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresHallazgoAuditoria_ElementoTrazadoId",
                table: "LugaresHallazgoAuditoria",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresHallazgoAuditoria_HallazgoAuditoriaId",
                table: "LugaresHallazgoAuditoria",
                column: "HallazgoAuditoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresHallazgoAuditoria_ItemCatalogoId",
                table: "LugaresHallazgoAuditoria",
                column: "ItemCatalogoId");

            migrationBuilder.CreateIndex(
                name: "IX_LugaresHallazgoAuditoria_TiroId",
                table: "LugaresHallazgoAuditoria",
                column: "TiroId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosHallazgoAuditoria");

            migrationBuilder.DropTable(
                name: "CambiosAuditoria");

            migrationBuilder.DropTable(
                name: "HallazgosPlan");

            migrationBuilder.DropTable(
                name: "LugaresAuditoria");

            migrationBuilder.DropTable(
                name: "LugaresHallazgoAuditoria");

            migrationBuilder.DropTable(
                name: "HallazgosAuditoria");

            migrationBuilder.DropTable(
                name: "Auditorias");

            migrationBuilder.DropTable(
                name: "ProgramasAuditoria");

            migrationBuilder.DropColumn(
                name: "Eficaz",
                table: "PlanesAccion");

            migrationBuilder.DropColumn(
                name: "EvidenciaEficacia",
                table: "PlanesAccion");

            migrationBuilder.DropColumn(
                name: "FechaVerificacionEficacia",
                table: "PlanesAccion");

            migrationBuilder.DropColumn(
                name: "VerificoEficacia",
                table: "PlanesAccion");
        }
    }
}
