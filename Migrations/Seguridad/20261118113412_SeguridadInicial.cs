using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SigWeb.Migrations.Seguridad
{
    /// <inheritdoc />
    public partial class SeguridadInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    NombreCompleto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Cargo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Empresa = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Profesion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MatriculaProfesional = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    TelefonoFijo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CorreoAlterno = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Ciudad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    JefeInmediato = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    FotoVersion = table.Column<long>(type: "bigint", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UltimoIngreso = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProyectoActivoId = table.Column<int>(type: "integer", nullable: true),
                    AutorizoDatosEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    VersionPolitica = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Anonimizado = table.Column<bool>(type: "boolean", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Evento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Exito = table.Column<bool>(type: "boolean", nullable: false),
                    ProyectoId = table.Column<int>(type: "integer", nullable: true),
                    Ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Navegador = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Detalle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FotosUsuario",
                columns: table => new
                {
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Datos = table.Column<byte[]>(type: "bytea", nullable: false),
                    Actualizado = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FotosUsuario", x => x.UsuarioId);
                });

            migrationBuilder.CreateTable(
                name: "Invitaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProyectoId = table.Column<int>(type: "integer", nullable: true),
                    RolSigId = table.Column<int>(type: "integer", nullable: true),
                    AdministradorGlobal = table.Column<bool>(type: "boolean", nullable: false),
                    AccesoVence = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Vence = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreadaPor = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    AceptadaEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Mensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Envios = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificacionesEnviadas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProyectoId = table.Column<int>(type: "integer", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Asunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Html = table.Column<string>(type: "text", nullable: true),
                    Claves = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Elementos = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Intentos = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Creado = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Enviado = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EnviadoPor = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificacionesEnviadas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreferenciasNotificacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Recibir = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreferenciasNotificacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Proyectos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Cliente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Ubicacion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BaseDatos = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    EsEmpresa = table.Column<bool>(type: "boolean", nullable: false),
                    EsPiloto = table.Column<bool>(type: "boolean", nullable: false),
                    Color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    FechaInicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FechaFin = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proyectos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReglasNotificacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProyectoId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Activa = table.Column<bool>(type: "boolean", nullable: false),
                    Frecuencia = table.Column<int>(type: "integer", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    DiaMes = table.Column<int>(type: "integer", nullable: false),
                    Hora = table.Column<int>(type: "integer", nullable: false),
                    Destino = table.Column<int>(type: "integer", nullable: false),
                    RolSigId = table.Column<int>(type: "integer", nullable: true),
                    UsuariosIds = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Correos = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SoloNuevos = table.Column<bool>(type: "boolean", nullable: false),
                    UltimaEjecucion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ModificadoPor = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ModificadoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglasNotificacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolesSig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    DeFabrica = table.Column<bool>(type: "boolean", nullable: false),
                    Clave = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Orden = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolesSig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudesAcceso",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Empresa = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    RevisadaPor = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    RevisadaEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesAcceso", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserPasskeys",
                columns: table => new
                {
                    CredentialId = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserPasskeys", x => x.CredentialId);
                    table.ForeignKey(
                        name: "FK_AspNetUserPasskeys_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VinculosTelegram",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ProyectoId = table.Column<int>(type: "integer", nullable: false),
                    UsuarioTelegram = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Vinculado = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UltimoUso = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VinculosTelegram", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VinculosTelegram_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModulosProyecto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProyectoId = table.Column<int>(type: "integer", nullable: false),
                    Modulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    Fuente = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModulosProyecto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModulosProyecto_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Miembros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ProyectoId = table.Column<int>(type: "integer", nullable: false),
                    RolSigId = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Vence = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AsignadoPor = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Miembros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Miembros_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Miembros_Proyectos_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "Proyectos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Miembros_RolesSig_RolSigId",
                        column: x => x.RolSigId,
                        principalTable: "RolesSig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PermisosRol",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RolSigId = table.Column<int>(type: "integer", nullable: false),
                    Modulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Acciones = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisosRol", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermisosRol_RolesSig_RolSigId",
                        column: x => x.RolSigId,
                        principalTable: "RolesSig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermisosMiembro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MiembroProyectoId = table.Column<int>(type: "integer", nullable: false),
                    Modulo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Conceder = table.Column<int>(type: "integer", nullable: false),
                    Negar = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisosMiembro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermisosMiembro_Miembros_MiembroProyectoId",
                        column: x => x.MiembroProyectoId,
                        principalTable: "Miembros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserPasskeys_UserId",
                table: "AspNetUserPasskeys",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Evento_Fecha",
                table: "Auditoria",
                columns: new[] { "Evento", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Fecha",
                table: "Auditoria",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_UsuarioId_Fecha",
                table: "Auditoria",
                columns: new[] { "UsuarioId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitaciones_Email_Estado",
                table: "Invitaciones",
                columns: new[] { "Email", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitaciones_TokenHash",
                table: "Invitaciones",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Miembros_ProyectoId",
                table: "Miembros",
                column: "ProyectoId");

            migrationBuilder.CreateIndex(
                name: "IX_Miembros_RolSigId",
                table: "Miembros",
                column: "RolSigId");

            migrationBuilder.CreateIndex(
                name: "IX_Miembros_UsuarioId_ProyectoId",
                table: "Miembros",
                columns: new[] { "UsuarioId", "ProyectoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModulosProyecto_ProyectoId_Modulo",
                table: "ModulosProyecto",
                columns: new[] { "ProyectoId", "Modulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificacionesEnviadas_Creado",
                table: "NotificacionesEnviadas",
                column: "Creado");

            migrationBuilder.CreateIndex(
                name: "IX_NotificacionesEnviadas_Estado_Creado",
                table: "NotificacionesEnviadas",
                columns: new[] { "Estado", "Creado" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificacionesEnviadas_ProyectoId_Tipo_Email",
                table: "NotificacionesEnviadas",
                columns: new[] { "ProyectoId", "Tipo", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_PermisosMiembro_MiembroProyectoId_Modulo",
                table: "PermisosMiembro",
                columns: new[] { "MiembroProyectoId", "Modulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermisosRol_RolSigId_Modulo",
                table: "PermisosRol",
                columns: new[] { "RolSigId", "Modulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreferenciasNotificacion_UsuarioId_Tipo",
                table: "PreferenciasNotificacion",
                columns: new[] { "UsuarioId", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proyectos_BaseDatos",
                table: "Proyectos",
                column: "BaseDatos",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proyectos_Codigo",
                table: "Proyectos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReglasNotificacion_ProyectoId_Tipo",
                table: "ReglasNotificacion",
                columns: new[] { "ProyectoId", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolesSig_Nombre",
                table: "RolesSig",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesAcceso_Estado_Fecha",
                table: "SolicitudesAcceso",
                columns: new[] { "Estado", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_VinculosTelegram_ChatId",
                table: "VinculosTelegram",
                column: "ChatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VinculosTelegram_UsuarioId",
                table: "VinculosTelegram",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserPasskeys");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "FotosUsuario");

            migrationBuilder.DropTable(
                name: "Invitaciones");

            migrationBuilder.DropTable(
                name: "ModulosProyecto");

            migrationBuilder.DropTable(
                name: "NotificacionesEnviadas");

            migrationBuilder.DropTable(
                name: "PermisosMiembro");

            migrationBuilder.DropTable(
                name: "PermisosRol");

            migrationBuilder.DropTable(
                name: "PreferenciasNotificacion");

            migrationBuilder.DropTable(
                name: "ReglasNotificacion");

            migrationBuilder.DropTable(
                name: "SolicitudesAcceso");

            migrationBuilder.DropTable(
                name: "VinculosTelegram");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Miembros");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Proyectos");

            migrationBuilder.DropTable(
                name: "RolesSig");
        }
    }
}
