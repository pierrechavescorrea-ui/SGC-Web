using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations
{
    /// <inheritdoc />
    public partial class CostosPresupuestos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActasFacturacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodoDesde = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PeriodoHasta = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Observaciones = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActasFacturacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AjustesPrecio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Anio = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Soporte = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AjustesPrecio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CodigosPresupuesto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Grupo = table.Column<int>(type: "integer", nullable: false),
                    PresupuestoInicial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigosPresupuesto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionCostos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Cliente = table.Column<string>(type: "text", nullable: true),
                    Contrato = table.Column<string>(type: "text", nullable: true),
                    Interventor = table.Column<string>(type: "text", nullable: true),
                    Objeto = table.Column<string>(type: "text", nullable: true),
                    Inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Fin = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ValorContrato = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ValorizarPrecioActual = table.Column<bool>(type: "boolean", nullable: false),
                    CriterioReparto = table.Column<int>(type: "integer", nullable: false),
                    CuadrillaAdministracion = table.Column<string>(type: "text", nullable: false),
                    CuadrillasSinRepartoAdmin = table.Column<string>(type: "text", nullable: true),
                    FactorPrestacional = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    RecargoA = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    RecargoB = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    BaseAdicional = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DivisorAdicional = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionCostos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EstadosTorreManual",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Desde = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Nota = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosTorreManual", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstadosTorreManual_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemsPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    Unidad = table.Column<string>(type: "text", nullable: true),
                    CapituloId = table.Column<int>(type: "integer", nullable: true),
                    EsCapitulo = table.Column<bool>(type: "boolean", nullable: false),
                    CantidadContratada = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioBase = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    AnioBase = table.Column<int>(type: "integer", nullable: false),
                    AiuPct = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Ambito = table.Column<int>(type: "integer", nullable: false),
                    AplicaOverhead = table.Column<bool>(type: "boolean", nullable: false),
                    AplicaSostenimiento = table.Column<bool>(type: "boolean", nullable: false),
                    AplicaImpuestos = table.Column<bool>(type: "boolean", nullable: false),
                    CostoUnitarioPresupuestado = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    CuadrillaPorDefecto = table.Column<string>(type: "text", nullable: true),
                    ActividadObraId = table.Column<int>(type: "integer", nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemsPago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemsPago_ActividadesObra_ActividadObraId",
                        column: x => x.ActividadObraId,
                        principalTable: "ActividadesObra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ItemsPago_ItemsPago_CapituloId",
                        column: x => x.CapituloId,
                        principalTable: "ItemsPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MetasPeriodo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Desde = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Hasta = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    InicioSemana1 = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    MetaFacturacion = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MetaProduccionTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    Vigente = table.Column<bool>(type: "boolean", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetasPeriodo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModificacionesContrato",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Numero = table.Column<string>(type: "text", nullable: true),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    RutaSoporte = table.Column<string>(type: "text", nullable: true),
                    NombreSoporte = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModificacionesContrato", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PorcentajesIndirectos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Concepto = table.Column<int>(type: "integer", nullable: false),
                    Desde = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Porcentaje = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Nota = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PorcentajesIndirectos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgramasTrabajo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    LineaBase = table.Column<bool>(type: "boolean", nullable: false),
                    Vigente = table.Column<bool>(type: "boolean", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    AprobadoPor = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramasTrabajo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComponentesAjustePrecio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AjustePrecioId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Peso = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Indice = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentesAjustePrecio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComponentesAjustePrecio_AjustesPrecio_AjustePrecioId",
                        column: x => x.AjustePrecioId,
                        principalTable: "AjustesPrecio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Personas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CodigoPresupuestoId = table.Column<int>(type: "integer", nullable: true),
                    Cargo = table.Column<string>(type: "text", nullable: true),
                    Cedula = table.Column<string>(type: "text", nullable: true),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Ingreso = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Retiro = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Salario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Auxilio = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Cuadrilla = table.Column<string>(type: "text", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Personas_CodigosPresupuesto_CodigoPresupuestoId",
                        column: x => x.CodigoPresupuestoId,
                        principalTable: "CodigosPresupuesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ValoresPresupuesto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CodigoPresupuestoId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoresPresupuesto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValoresPresupuesto_CodigosPresupuesto_CodigoPresupuestoId",
                        column: x => x.CodigoPresupuestoId,
                        principalTable: "CodigosPresupuesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionesCostoActividad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Cuadrilla = table.Column<string>(type: "text", nullable: false),
                    ItemPagoId = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesCostoActividad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionesCostoActividad_ItemsPago_ItemPagoId",
                        column: x => x.ItemPagoId,
                        principalTable: "ItemsPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CantidadesTorre",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: false),
                    ComponenteId = table.Column<int>(type: "integer", nullable: true),
                    ItemPagoId = table.Column<int>(type: "integer", nullable: false),
                    CantidadPrevista = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Cimentacion = table.Column<string>(type: "text", nullable: true),
                    TipoTorre = table.Column<string>(type: "text", nullable: true),
                    Cuerpo = table.Column<string>(type: "text", nullable: true),
                    Nota = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CantidadesTorre", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CantidadesTorre_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CantidadesTorre_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CantidadesTorre_ItemsPago_ItemPagoId",
                        column: x => x.ItemPagoId,
                        principalTable: "ItemsPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostosOrden",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroOrden = table.Column<string>(type: "text", nullable: true),
                    CodigoPresupuestoId = table.Column<int>(type: "integer", nullable: true),
                    Proveedor = table.Column<string>(type: "text", nullable: true),
                    Concepto = table.Column<string>(type: "text", nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Desde = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Hasta = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Cuadrilla = table.Column<string>(type: "text", nullable: true),
                    Pf1 = table.Column<bool>(type: "boolean", nullable: false),
                    ItemPagoId = table.Column<int>(type: "integer", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostosOrden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostosOrden_CodigosPresupuesto_CodigoPresupuestoId",
                        column: x => x.CodigoPresupuestoId,
                        principalTable: "CodigosPresupuesto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CostosOrden_ItemsPago_ItemPagoId",
                        column: x => x.ItemPagoId,
                        principalTable: "ItemsPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LineasActaFacturacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActaFacturacionId = table.Column<int>(type: "integer", nullable: false),
                    ItemPagoId = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasActaFacturacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasActaFacturacion_ActasFacturacion_ActaFacturacionId",
                        column: x => x.ActaFacturacionId,
                        principalTable: "ActasFacturacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LineasActaFacturacion_ItemsPago_ItemPagoId",
                        column: x => x.ItemPagoId,
                        principalTable: "ItemsPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreciosManuales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ItemPagoId = table.Column<int>(type: "integer", nullable: false),
                    Anio = table.Column<int>(type: "integer", nullable: false),
                    Precio = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreciosManuales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreciosManuales_ItemsPago_ItemPagoId",
                        column: x => x.ItemPagoId,
                        principalTable: "ItemsPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetasProduccionDia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MetaPeriodoId = table.Column<int>(type: "integer", nullable: false),
                    Cuadrilla = table.Column<string>(type: "text", nullable: false),
                    ItemPagoId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetasProduccionDia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetasProduccionDia_ItemsPago_ItemPagoId",
                        column: x => x.ItemPagoId,
                        principalTable: "ItemsPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetasProduccionDia_MetasPeriodo_MetaPeriodoId",
                        column: x => x.MetaPeriodoId,
                        principalTable: "MetasPeriodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LineasModificacionContrato",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModificacionContratoId = table.Column<int>(type: "integer", nullable: false),
                    ItemPagoId = table.Column<int>(type: "integer", nullable: false),
                    CantidadAntes = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    CantidadDespues = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PrecioAntes = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PrecioDespues = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    AnioPrecio = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasModificacionContrato", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasModificacionContrato_ItemsPago_ItemPagoId",
                        column: x => x.ItemPagoId,
                        principalTable: "ItemsPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LineasModificacionContrato_ModificacionesContrato_Modificac~",
                        column: x => x.ModificacionContratoId,
                        principalTable: "ModificacionesContrato",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActividadesPrograma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProgramaTrabajoId = table.Column<int>(type: "integer", nullable: false),
                    ItemPagoId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    TiroId = table.Column<int>(type: "integer", nullable: true),
                    Cuadrilla = table.Column<string>(type: "text", nullable: true),
                    Inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Fin = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Predecesoras = table.Column<string>(type: "text", nullable: true),
                    Nota = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActividadesPrograma", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActividadesPrograma_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActividadesPrograma_ItemsPago_ItemPagoId",
                        column: x => x.ItemPagoId,
                        principalTable: "ItemsPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActividadesPrograma_ProgramasTrabajo_ProgramaTrabajoId",
                        column: x => x.ProgramaTrabajoId,
                        principalTable: "ProgramasTrabajo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActividadesPrograma_Tiros_TiroId",
                        column: x => x.TiroId,
                        principalTable: "Tiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CambiosPrograma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProgramaTrabajoId = table.Column<int>(type: "integer", nullable: false),
                    ActividadProgramaId = table.Column<int>(type: "integer", nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Campo = table.Column<string>(type: "text", nullable: false),
                    Antes = table.Column<string>(type: "text", nullable: true),
                    Despues = table.Column<string>(type: "text", nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    Usuario = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosPrograma", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CambiosPrograma_ProgramasTrabajo_ProgramaTrabajoId",
                        column: x => x.ProgramaTrabajoId,
                        principalTable: "ProgramasTrabajo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosCantidad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ItemPagoId = table.Column<int>(type: "integer", nullable: false),
                    ElementoTrazadoId = table.Column<int>(type: "integer", nullable: true),
                    ComponenteId = table.Column<int>(type: "integer", nullable: true),
                    TiroId = table.Column<int>(type: "integer", nullable: true),
                    Cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Cuadrilla = table.Column<string>(type: "text", nullable: true),
                    Descripcion = table.Column<string>(type: "text", nullable: true),
                    ActividadProgramaId = table.Column<int>(type: "integer", nullable: true),
                    Origen = table.Column<string>(type: "text", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaEliminacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosCantidad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosCantidad_ActividadesPrograma_ActividadProgramaId",
                        column: x => x.ActividadProgramaId,
                        principalTable: "ActividadesPrograma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RegistrosCantidad_Componentes_ComponenteId",
                        column: x => x.ComponenteId,
                        principalTable: "Componentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistrosCantidad_ElementosTrazado_ElementoTrazadoId",
                        column: x => x.ElementoTrazadoId,
                        principalTable: "ElementosTrazado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistrosCantidad_ItemsPago_ItemPagoId",
                        column: x => x.ItemPagoId,
                        principalTable: "ItemsPago",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosCantidad_Tiros_TiroId",
                        column: x => x.TiroId,
                        principalTable: "Tiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesPrograma_ElementoTrazadoId",
                table: "ActividadesPrograma",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesPrograma_ItemPagoId",
                table: "ActividadesPrograma",
                column: "ItemPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesPrograma_ProgramaTrabajoId",
                table: "ActividadesPrograma",
                column: "ProgramaTrabajoId");

            migrationBuilder.CreateIndex(
                name: "IX_ActividadesPrograma_TiroId",
                table: "ActividadesPrograma",
                column: "TiroId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesCostoActividad_ItemPagoId",
                table: "AsignacionesCostoActividad",
                column: "ItemPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_CambiosPrograma_ProgramaTrabajoId",
                table: "CambiosPrograma",
                column: "ProgramaTrabajoId");

            migrationBuilder.CreateIndex(
                name: "IX_CantidadesTorre_ComponenteId",
                table: "CantidadesTorre",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_CantidadesTorre_ElementoTrazadoId_ItemPagoId",
                table: "CantidadesTorre",
                columns: new[] { "ElementoTrazadoId", "ItemPagoId" });

            migrationBuilder.CreateIndex(
                name: "IX_CantidadesTorre_ItemPagoId",
                table: "CantidadesTorre",
                column: "ItemPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_ComponentesAjustePrecio_AjustePrecioId",
                table: "ComponentesAjustePrecio",
                column: "AjustePrecioId");

            migrationBuilder.CreateIndex(
                name: "IX_CostosOrden_CodigoPresupuestoId",
                table: "CostosOrden",
                column: "CodigoPresupuestoId");

            migrationBuilder.CreateIndex(
                name: "IX_CostosOrden_ItemPagoId",
                table: "CostosOrden",
                column: "ItemPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_EstadosTorreManual_ElementoTrazadoId",
                table: "EstadosTorreManual",
                column: "ElementoTrazadoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemsPago_ActividadObraId",
                table: "ItemsPago",
                column: "ActividadObraId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemsPago_CapituloId",
                table: "ItemsPago",
                column: "CapituloId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemsPago_Codigo",
                table: "ItemsPago",
                column: "Codigo");

            migrationBuilder.CreateIndex(
                name: "IX_LineasActaFacturacion_ActaFacturacionId",
                table: "LineasActaFacturacion",
                column: "ActaFacturacionId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasActaFacturacion_ItemPagoId",
                table: "LineasActaFacturacion",
                column: "ItemPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasModificacionContrato_ItemPagoId",
                table: "LineasModificacionContrato",
                column: "ItemPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasModificacionContrato_ModificacionContratoId",
                table: "LineasModificacionContrato",
                column: "ModificacionContratoId");

            migrationBuilder.CreateIndex(
                name: "IX_MetasProduccionDia_ItemPagoId",
                table: "MetasProduccionDia",
                column: "ItemPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_MetasProduccionDia_MetaPeriodoId",
                table: "MetasProduccionDia",
                column: "MetaPeriodoId");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_CodigoPresupuestoId",
                table: "Personas",
                column: "CodigoPresupuestoId");

            migrationBuilder.CreateIndex(
                name: "IX_PreciosManuales_ItemPagoId_Anio",
                table: "PreciosManuales",
                columns: new[] { "ItemPagoId", "Anio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosCantidad_ActividadProgramaId",
                table: "RegistrosCantidad",
                column: "ActividadProgramaId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosCantidad_ComponenteId",
                table: "RegistrosCantidad",
                column: "ComponenteId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosCantidad_ElementoTrazadoId_ItemPagoId",
                table: "RegistrosCantidad",
                columns: new[] { "ElementoTrazadoId", "ItemPagoId" });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosCantidad_Fecha",
                table: "RegistrosCantidad",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosCantidad_ItemPagoId",
                table: "RegistrosCantidad",
                column: "ItemPagoId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosCantidad_TiroId",
                table: "RegistrosCantidad",
                column: "TiroId");

            migrationBuilder.CreateIndex(
                name: "IX_ValoresPresupuesto_CodigoPresupuestoId",
                table: "ValoresPresupuesto",
                column: "CodigoPresupuestoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignacionesCostoActividad");

            migrationBuilder.DropTable(
                name: "CambiosPrograma");

            migrationBuilder.DropTable(
                name: "CantidadesTorre");

            migrationBuilder.DropTable(
                name: "ComponentesAjustePrecio");

            migrationBuilder.DropTable(
                name: "ConfiguracionCostos");

            migrationBuilder.DropTable(
                name: "CostosOrden");

            migrationBuilder.DropTable(
                name: "EstadosTorreManual");

            migrationBuilder.DropTable(
                name: "LineasActaFacturacion");

            migrationBuilder.DropTable(
                name: "LineasModificacionContrato");

            migrationBuilder.DropTable(
                name: "MetasProduccionDia");

            migrationBuilder.DropTable(
                name: "Personas");

            migrationBuilder.DropTable(
                name: "PorcentajesIndirectos");

            migrationBuilder.DropTable(
                name: "PreciosManuales");

            migrationBuilder.DropTable(
                name: "RegistrosCantidad");

            migrationBuilder.DropTable(
                name: "ValoresPresupuesto");

            migrationBuilder.DropTable(
                name: "AjustesPrecio");

            migrationBuilder.DropTable(
                name: "ActasFacturacion");

            migrationBuilder.DropTable(
                name: "ModificacionesContrato");

            migrationBuilder.DropTable(
                name: "MetasPeriodo");

            migrationBuilder.DropTable(
                name: "ActividadesPrograma");

            migrationBuilder.DropTable(
                name: "CodigosPresupuesto");

            migrationBuilder.DropTable(
                name: "ItemsPago");

            migrationBuilder.DropTable(
                name: "ProgramasTrabajo");
        }
    }
}
