# SGC Web

Aplicación web para la gestión técnica, de calidad (QA/QC) y del sistema integrado de gestión en proyectos de
líneas de transmisión eléctrica y subestaciones: trazado y tabla de torres sobre mapa, modelos 3D e IFC de torres,
avance de obra, ensayos, materiales, equipos, planos, documentos, riesgos, planes de acción, auditorías, indicadores,
costos y un esquema multiproyecto con permisos por rol.

**Este repositorio es una versión de demostración.** Al primer arranque crea, sobre una base PostgreSQL vacía,
la Empresa y dos proyectos de ejemplo con torres BP6, el modelo IFC BP6, documentos PDF y un registro de ejemplo por
módulo. Todos los datos son ficticios.

## Requisitos

- .NET 10 SDK
- PostgreSQL 16 o superior (el usuario necesita permiso para crear bases)

## Ejecutar en local

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=sgcweb_demo;Username=postgres;Password=<clave>"
dotnet run
```

En desarrollo el acceso es libre desde el propio equipo (`http://localhost:5265`). Los datos de demostración se
siembran solos (`Demo:Sembrar = true` en `appsettings.json`).

## Despliegue

Contenedor (`Dockerfile`) y guía para AWS (ECS Fargate, RDS PostgreSQL, EFS, SES) en `docs/aws-despliegue.md` y
`deploy/aws/`.

## Licencia

Software propietario. © 2026 Pierr Angelo Chaves Correa. Todos los derechos reservados. Ver `LICENSE` y
`TERCEROS.md`. El código se publica solo para consulta; cualquier otro uso requiere autorización escrita del titular.
