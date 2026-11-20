# SigWeb en AWS: guía de despliegue

Estado: la app quedó preparada (configuración por entorno, secretos fuera del repositorio, contenedor, salud,
balanceador, correo SES). Lo marcado **[decidir]** son supuestos que debe confirmar quien administre la cuenta de AWS.

## 1. Arquitectura recomendada

```
Usuarios ──HTTPS──► Route 53 ─► ALB (certificado ACM, WebSockets, sesiones pegajosas)
                                 │  grupo de seguridad: 443 desde Internet
                                 ▼
                       ECS Fargate (contenedor SigWeb, puerto 8080, 2 tareas)
                       │   grupo de seguridad: 8080 SOLO desde el ALB
                       ├──► RDS PostgreSQL 16+ (cifrado, copias automáticas, sin acceso público)
                       ├──► EFS montado en /app/wwwroot/uploads (archivos subidos, cifrado)
                       ├──► SES (correo por SMTP, puerto 587)
                       └──► Secrets Manager / SSM (cadenas de conexión, claves)
```

- **Región [decidir]:** `us-east-1` o `sa-east-1` (São Paulo, menor latencia desde Colombia). La política de datos debe nombrarla.
- **Blazor Server necesita WebSockets y sesiones pegajosas** en el ALB (stickiness por cookie de aplicación, 1 día).
  Con varias tareas, las claves de cifrado se comparten solas: están en la base central (Data Protection).
- Alternativa más simple para el piloto: **Elastic Beanstalk** con plataforma Docker, una instancia `t3.medium`, mismo RDS y EFS.

## 2. Bases de datos

Un servidor RDS con varias bases (mismo usuario y clave):

| Base | Para qué | Quién la crea |
|---|---|---|
| `sigweb` (o el nombre de `DefaultConnection`) | Proyecto piloto (la base de hoy) | Se restaura desde la copia actual |
| `sigweb_central` | Usuarios, proyectos, roles, permisos, auditoría, claves de cifrado | La app al arrancar (migraciones automáticas) |
| `sigweb_empresa` | Módulos compartidos (Direccionamiento, Certificaciones, Manuales) | La app la primera vez que se abre la Empresa |
| `sigweb_<código>` | Cada proyecto nuevo | Administración › Proyectos › Guardar |

- El usuario de la base necesita permiso `CREATEDB` (crea las bases de los proyectos nuevos). Si la política de la empresa
  no lo permite, crear cada base a mano antes de dar de alta el proyecto.
- `Multiproyecto:MigrarAlPreparar = true` (producción): al abrir un proyecto por primera vez tras desplegar, se aplican sus
  migraciones pendientes. Hacer **copia (snapshot) de RDS antes de cada despliegue** con migraciones.
- Cadena por proyecto distinta (otro servidor): variable `ConnectionStrings__Proyecto_<CODIGO>`.

## 3. Variables de entorno y secretos (nunca en appsettings ni en Git)

En la definición de tarea de ECS, como `secrets` desde Secrets Manager / SSM:

| Variable | Ejemplo / nota |
|---|---|
| `ConnectionStrings__DefaultConnection` | `Host=xxx.rds.amazonaws.com;Database=sigweb;Username=sigweb;Password=***;SSL Mode=Require` |
| `ConnectionStrings__Central` | Opcional; por defecto la misma con base `sigweb_central` |
| `Correo__Smtp__Usuario` / `Correo__Smtp__Clave` | Credenciales SMTP de SES (no son las claves IAM) |
| `Correo__Remitente` | `SigWeb <no-responder@dominio-verificado>` |
| `Telegram__Token` | Token del bot (BotFather). Solo UNA instancia puede leer el bot: ver §7 |
| `Seguridad__AdministradorInicial` | Correo del primer administrador (solo el primer arranque) |
| `Aplicacion__UrlPublica` | `https://sigweb.dominio.com` (los enlaces de los correos salen de aquí) |
| `AllowedHosts` | `sigweb.dominio.com` |

Variables normales: `ASPNETCORE_ENVIRONMENT=Production`, `TZ=America/Bogota`.

## 4. Primer arranque

1. Restaurar la base actual en RDS como `sigweb` (pg_dump/pg_restore).
2. Copiar `wwwroot/uploads` del equipo actual al EFS (`aws s3 cp` a un bucket temporal + `rsync` desde una instancia con el EFS montado).
3. Desplegar con `Seguridad__AdministradorInicial=correo@empresa.com`. La app crea la base central, los roles, el proyecto
   PILOTO (la base existente) y la EMPRESA, y envía la invitación al administrador. Si el correo aún no funciona, el enlace
   queda en el registro de la app (CloudWatch Logs), una sola vez.
4. El administrador crea su contraseña, activa la verificación en dos pasos (obligatoria para administradores) e invita al resto.
5. Quitar `Seguridad__AdministradorInicial` de la tarea.

## 5. Correo (SES)

1. Verificar el dominio en SES (DKIM + SPF + DMARC en Route 53).
2. Pedir salida de la *sandbox* de SES (si no, solo llega a correos verificados).
3. Crear credenciales SMTP (SES › SMTP settings) → `Correo__Smtp__Usuario` / `Correo__Smtp__Clave`.
4. `Correo:Smtp:Servidor = email-smtp.<región>.amazonaws.com`, puerto 587 (STARTTLS).
5. Probar desde Administración › Sistema › correo de prueba.

## 6. Balanceador (ALB)

- Escucha 443 con certificado de ACM; 80 → redirige a 443.
- Grupo de destino HTTP 8080, **stickiness** activada, comprobación de salud `GET /salud` (200), intervalo 30 s.
- `/salud/listo` comprueba también las bases (útil para alarmas, no para el ALB).
- Tiempo de inactividad del ALB: 3600 s (circuitos de Blazor y cargas largas).
- `Proxy:ConfiarEnCualquierOrigen = true` (appsettings.Production.json) SOLO porque el contenedor no es alcanzable sin
  pasar por el ALB (grupo de seguridad). `Proxy:Saltos = 1` (si se pone CloudFront delante: 2).
- AWS WAF en el ALB **[recomendado]**: reglas administradas *Core rule set*, *Known bad inputs*, *IP reputation* y una regla de
  tasa (p. ej. 300 peticiones / 5 min por IP en `/cuenta/*`). La app ya limita 10 intentos/min por IP en el login.

## 7. Bot de Telegram

Telegram entrega los mensajes a UNA sola conexión por token (long polling). Con 2+ tareas: correr el bot solo en una
(variable `Telegram__Token` solo en un servicio ECS aparte de 1 tarea, o dejar 1 tarea en total para el piloto).

## 8. Seguridad de la cuenta AWS (mínimo)

- RDS y EFS cifrados con KMS; RDS sin acceso público; copias automáticas 14 días + snapshot antes de cada despliegue.
- Rol de tarea de ECS con permisos mínimos (leer sus secretos, escribir en CloudWatch Logs). Nada de claves IAM en variables.
- CloudTrail y GuardDuty activados; alarma de CloudWatch por errores 5xx y por eventos «Cuenta bloqueada» en los logs.
- MFA para todos los usuarios de la consola de AWS.

## 9. Desplegar una versión nueva

```bash
docker build -t sigweb:$(git rev-parse --short HEAD) .
aws ecr get-login-password | docker login --username AWS --password-stdin <cuenta>.dkr.ecr.<región>.amazonaws.com
docker tag sigweb:<tag> <cuenta>.dkr.ecr.<región>.amazonaws.com/sigweb:<tag>
docker push <cuenta>.dkr.ecr.<región>.amazonaws.com/sigweb:<tag>
# Snapshot de RDS → actualizar la definición de tarea con la imagen nueva → servicio ECS: forzar despliegue nuevo
```

## 10. Antes de salir a producción (lista)

- [ ] `appsettings.Production.json`: reemplazar `sigweb.example.com` por el dominio real.
- [ ] Política de tratamiento de datos (`/privacidad`) revisada por el área jurídica; `Privacidad:Contacto` definido.
- [ ] Revisar los avisos de CSP en la auditoría (`/csp-reporte`) y pasar `Seguridad:CspSoloReporte` a `false`.
- [ ] `Telegram:PermitirChatsSinUsuario = false` ya está en producción: volver a vincular cada chat desde Mi cuenta.
- [ ] Prueba de restauración de una copia de RDS.

## 11. Archivos de despliegue (`deploy/aws/`)

- `ecs-task-definition.json`: plantilla de la tarea Fargate (1 vCPU, 2 GB, puerto 8080, secretos desde Secrets Manager,
  EFS en `/app/wwwroot/uploads` y `/app/App_Data` con cifrado en tránsito, logs en CloudWatch `/ecs/sigweb`). Copiarla, reemplazar
  `<DOMINIO>`, `<EFS_ID>`, `<EFS_AP_*>`, `<CORREO_ADMIN>` y los roles; `<CUENTA>`, `<REGION>` y `<ETIQUETA>` los pone el script.
- Secretos esperados: `sigweb/db` con la clave `DefaultConnection` y `sigweb/smtp` con `Usuario` y `Clave`.
- `desplegar.sh`: construye la imagen, la sube a ECR (crea el repositorio la primera vez), pide el snapshot de RDS
  (`RDS_INSTANCIA=…`), registra la definición y fuerza el despliegue esperando a que el servicio quede estable.
  `CUENTA=… REGION=sa-east-1 TAREA=mi-tarea.json RDS_INSTANCIA=sigweb-db ./deploy/aws/desplegar.sh`
- El contenedor no trae `curl`/`wget`: la salud la comprueba el ALB (`GET /salud`), no la tarea.
- Tras el primer ingreso del administrador, quitar `Seguridad__AdministradorInicial` de la tarea y volver a desplegar.

## 12. SGC-Web (copia de demostración): datos de demostración

Este repositorio (SGC-Web) arranca con `Demo:Sembrar = true` (appsettings.json) y, sobre un RDS vacío, deja la app lista para probar:
- **Empresa** (SGC administrador) con Direccionamiento (Visión, Política integrada), Certificación ISO 9001 y documentos.
- **LTN230** «LT Norte 230 kV (demo)» (base = la de `DefaultConnection`) y **LTS500** «LT Sur 500 kV (demo)» (base `<base>_lts500`).
- En cada proyecto: IFC **BP6** cargado (v1), **10 torres BP6** sobre un trazado ficticio en 2 tiros, hoja de vida + patas,
  PDF en la carpeta «Adjuntos» de la hoja de vida y en Archivos de la hoja de vida y de la pata A, y un ejemplo por módulo
  (riesgos, planes, auditoría, comunicados, documentos, planos, equipos, materiales, ensayos, Matriz PIE, avance de obra, costos,
  indicadores, organigrama, partes interesadas, un registro en la Papelera).
- Código en `Data/Demo/` (una clase `IModuloDemo` por módulo, idempotente: si la tabla tiene datos no hace nada). Recursos
  (PDF y torres) en `Data/Demo/recursos`, se publican con la app.
- Sin datos de ejemplo: no aplica Manuales de funciones (página vacía), Creador de módulos (base central) ni Costos de no calidad
  (en una base nueva Planes de acción no tiene paso ligado a ese módulo).
- Para producción real: `Demo__Sembrar=false` en la tarea (o desplegar el repositorio original SigWeb). Los proyectos de prueba se dan de baja en
  Administración › Proyectos.
