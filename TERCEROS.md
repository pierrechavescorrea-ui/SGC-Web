# Componentes de terceros

SGC Web es propiedad de Pierr Angelo Chaves Correa (ver `LICENSE`). Usa las siguientes bibliotecas de terceros.
Cada una conserva su autor y su licencia, y ninguna impide el uso comercial de SGC Web.

## Paquetes NuGet (servidor)

| Componente | Licencia |
|---|---|
| .NET / ASP.NET Core / EF Core / Identity (Microsoft) | MIT |
| Npgsql y Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL License |
| ACadSharp | MIT |
| ClosedXML | MIT |
| PdfPig | Apache 2.0 |
| QRCoder | MIT |
| MailKit | MIT |
| SharpKml.Core | MIT |

## Bibliotecas web (`wwwroot/lib`)

| Componente | Licencia |
|---|---|
| Bootstrap | MIT |
| CesiumJS | Apache 2.0 |
| Chart.js | MIT |
| Leaflet | BSD 2-Clause |
| PDF.js (Mozilla) | Apache 2.0 |
| three.js | MIT |
| Univer | Apache 2.0 |
| web-ifc | MPL 2.0 |

## Notas

- **MPL 2.0 (web-ifc):** si se modifican los archivos de web-ifc, esos archivos modificados deben publicarse con
  la misma licencia. El resto de SGC Web no queda afectado. Hoy se usa sin modificar.
- **Apache 2.0 / MIT / BSD:** hay que conservar sus avisos de copyright y licencia (los archivos `LICENSE` de cada
  carpeta en `wwwroot/lib`).
- Al agregar una dependencia nueva, anotarla aquí. Si es GPL o AGPL, revisarla antes de agregarla.
