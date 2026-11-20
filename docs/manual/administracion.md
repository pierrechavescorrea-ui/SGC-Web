---
modulo: administracion
titulo: Administración
menu: Administración
rutas: [/admin, /admin/proyectos, /admin/usuarios, /admin/invitaciones, /admin/roles, /admin/notificaciones, /admin/auditoria, /admin/sistema, /proyectos]
palabras: [logo, marca, logo de la app, administración, administrador, usuarios, invitar, invitación, solicitudes de acceso, roles, permisos, matriz de permisos, proyectos, nuevo proyecto, eliminar proyecto, avisos por correo, notificaciones, auditoría de accesos, sistema, bloqueado, desbloquear, 2 pasos, tablero de proyectos]
opciones:
  - Administración: /admin
  - Eliminados: /admin/proyectos | Eliminados
  - Correo saliente: /admin/sistema | Correo saliente
  - Logo de la app: /admin/sistema | Logo de la app
  - Bitácora de envíos: /admin/notificaciones | Bitácora de envíos
  - Tablero de proyectos: /admin | Tablero de proyectos
  - Tablero: /admin
  - Invitar usuario: /admin/invitaciones
  - Nuevo proyecto: /admin/proyectos?nuevo=1
  - Entrar al proyecto: /admin | Entrar al proyecto
  - Proyectos: /admin/proyectos
  - Usuarios: /admin/usuarios
  - Invitaciones: /admin/invitaciones?vista=invitaciones
  - Roles: /admin/roles
  - Avisos por correo: /admin/notificaciones
  - Auditoría: /admin/auditoria
  - Sistema: /admin/sistema
  - Enviar invitación: /admin/invitaciones | Enviar invitación
  - Reenviar (enlace nuevo): /admin/invitaciones?vista=invitaciones | Reenviar (enlace nuevo)
  - Revocar: /admin/invitaciones?vista=invitaciones | Revocar
  - solicitudes de acceso: /admin/invitaciones?vista=solicitudes
  - Invitar a esta persona: /admin/invitaciones?vista=solicitudes | Invitar a esta persona
  - Rechazar: /admin/invitaciones?vista=solicitudes | Rechazar
  - matriz de permisos: /admin/roles
  - Volver a los permisos de fábrica de este rol: /admin/roles | Volver a los permisos de fábrica de este rol
  - Configurar: /admin/notificaciones | Configurar
  - 'Vista previa: qué se enviaría ahora y a quién: /admin/notificaciones | Vista previa: qué se enviaría ahora y a quién'
  - Cargar logo: /admin/sistema | Cargar logo: clic o arrastre la imagen aquí (PNG, JPG o WebP, máx. 2 MB)
  - Quitar logo: /admin/sistema | Quitar logo
  - Exportar a Excel lo filtrado: /admin/auditoria | Exportar a Excel lo filtrado
  - Enviarme un correo de prueba: /admin/sistema | Enviarme un correo de prueba
  - Proyectos y módulos: /admin/proyectos | Proyectos y módulos
  - Usuarios y acceso: /admin/usuarios
  - Usuarios y permisos: /admin/usuarios | Usuarios y permisos
  - Invitaciones y solicitudes: /admin/invitaciones | Invitaciones y solicitudes
  - Notificaciones: /admin/notificaciones
  - Bot de Telegram: /admin/telegram
  - Sistema y seguridad: /admin/sistema
  - Sistema y servidor: /admin/sistema | Sistema y servidor
  - Auditoría de accesos: /admin/auditoria | Auditoría de accesos
actualizado: 2026-11-19
---

# Administración

## Para qué sirve
Gestiona los **proyectos**, los **usuarios** y sus **permisos**, las **invitaciones**, los **avisos por correo** y la
**auditoría** de accesos. Solo la ven los administradores.

## Cómo entrar
Menú › **Administración**. El administrador general entra por defecto al **Tablero de proyectos** (`/proyectos`).

## Qué hay en la pantalla
El menú **Administración** tiene cinco módulos. Cuando un módulo tiene varias pantallas, use los botones de vista del encabezado.

| Módulo | Vistas |
|---|---|
| **Proyectos** | **Tablero de proyectos** (`/admin`: proyectos, usuarios, eventos de seguridad, IP con ingresos fallidos, **Invitar usuario**, **Nuevo proyecto**, **Entrar al proyecto**) · **Proyectos y módulos** |
| **Usuarios y acceso** | **Usuarios y permisos** · **Invitaciones y solicitudes** · **Roles** |
| **Notificaciones** | **Avisos por correo** |
| **Bot de Telegram** | Estado, enlace y QR, códigos y chats vinculados |
| **Sistema y seguridad** | **Sistema y servidor** · **Auditoría de accesos** |

El auditor solo ve **Sistema y seguridad** › **Auditoría de accesos**.

## Cómo hacer…

### Cómo invitar a una persona
1. **Invitar usuario** (en el Tablero o en Usuarios).
2. En el formulario **Invitar**, escriba el correo, el proyecto y el rol, y toque **Enviar invitación**.
3. Debajo del formulario, abra **Invitaciones** para **Reenviar (enlace nuevo)** o **Revocar**. Use **Copiar** en el aviso que muestra el enlace.
4. Abra **Solicitudes** para revisar las peticiones de acceso. **Invitar a esta persona** carga sus datos en el formulario. Revise el proyecto y el rol antes de enviar la invitación. También puede pulsar **Rechazar**.

### Cómo cambiar el rol o los permisos de un usuario
1. **Usuarios y acceso** › **Usuarios y permisos** › elija la persona.
2. En **Proyectos y rol**: **Dar acceso al proyecto**, cambiar el rol o **Quitar del proyecto**.
3. Para excepciones: **Permisos por módulo de este usuario en el proyecto** › marque lo que necesita › **Guardar permisos**.
   **Quitar todos los ajustes (solo el rol)** vuelve al rol puro.
4. **Ver la aplicación como este usuario (solo lectura)** sirve para comprobar qué ve.

### Cómo ayudar a un usuario bloqueado
En **Usuarios** › **Seguridad de la cuenta**: **Desbloquear**, **Enviar enlace para crear contraseña nueva**,
**Restablecer verificación en 2 pasos (teléfono perdido)** o **Cerrar todas sus sesiones**.

### Cómo ajustar un rol
**Usuarios y acceso** › **Roles**: la **matriz de permisos** por módulo y acción. **Rol nuevo (copia del seleccionado)**,
**Volver a los permisos de fábrica de este rol**, **Eliminar rol (solo sin usuarios)**.

### Cómo crear un proyecto
1. **Proyectos** › **Proyectos y módulos** › **Nuevo proyecto**: código, nombre, color, módulos habilitados (**Habilitar todos**).
2. **Crear o preparar la base (migraciones y catálogos de fábrica)**.
3. Invite a los usuarios.

**Eliminar proyecto** es una baja reversible (queda en **Eliminados**) y pide confirmación estricta: el código exacto,
una casilla, una espera y su contraseña. No se puede eliminar la Empresa, el proyecto abierto ni el último activo.

### Cómo configurar avisos por correo
**Notificaciones** › **Avisos por correo**: por cada tipo de aviso (calibraciones por vencer, acciones vencidas…) toque **Configurar**,
elija destinatarios y frecuencia y **Guardar**. **Vista previa: qué se enviaría ahora y a quién**,
**Enviar ahora** y **Bitácora de envíos**.

### Cómo cambiar el logo de la app
El logo es **uno para todos los proyectos** y solo lo cambia el administrador general.
1. **Administración** › **Sistema y seguridad** › **Sistema y servidor** › tarjeta **Logo de la app**.
2. **Cargar logo**: haga clic en el recuadro del logo o arrastre la imagen encima (PNG, JPG o WebP, máx. 2 MB). Se ve en el menú de todos los proyectos al cambiar de pantalla.
3. **Quitar logo** vuelve al nombre «SGC Web».

### Cómo revisar quién entró o qué hizo
**Sistema y seguridad** › **Auditoría de accesos**: filtre por fechas (**Desde** / **Hasta**) y **Exportar a Excel lo filtrado**.

## Permisos
Solo el **Administrador SGC** (todo en todos los proyectos, incluida esta Administración) y, en parte, el
**Administrador del proyecto**. **Sistema** (migraciones, correo saliente) es solo del administrador general.

## Problemas frecuentes
- **«El usuario no recibió la invitación»** → **Reenviar (enlace nuevo)** o **Copiar** el enlace y enviárselo.
- **«El usuario no ve un módulo»** → revise su rol, sus permisos por módulo y si **El módulo está apagado en este proyecto**.
- **«Perdió el teléfono de la verificación en dos pasos»** → **Restablecer verificación en 2 pasos**.
- **«Los correos no salen»** → **Sistema** › **Correo saliente** › **Enviarme un correo de prueba**.

## Preguntas frecuentes

### ¿Cómo doy acceso a una persona nueva?
Administración › Usuarios y acceso › **Invitaciones**: escriba el correo, elija el proyecto y el rol, y envíe la invitación.
La persona crea su contraseña desde el enlace del correo.

### ¿Cómo quito el acceso a alguien que se retiró?
En **Usuarios y permisos**, desactive al usuario o quítele la membresía del proyecto. Si se pide borrar sus datos personales,
use la opción de anonimizar: borra nombre, correo, cargo y empresa y cierra sus vínculos de Telegram (no se puede deshacer).

### ¿Qué diferencia hay entre un rol y un permiso individual?
El rol es la plantilla de permisos por módulo (Administración › **Roles**). Un usuario puede tener ajustes propios que
conceden o niegan algo puntual sobre su rol.

### ¿Cómo le doy permisos a un módulo creado con el Creador de módulos?
Después de publicarlo, en Administración › **Roles** aparece como un módulo más en la matriz: marque Ver, Crear, Editar,
etc. para cada rol.

### ¿Cómo cambio el logo de la app?
Administración › Sistema y seguridad › **Sistema**: el recuadro del logo es la zona de carga (clic o arrastrar). Es un solo
logo para todos los proyectos y también se ve en la pantalla de ingreso.

### ¿Cómo veo quién entró o quién hizo un cambio?
Administración › Sistema y seguridad › **Auditoría de accesos**: muestra ingresos, intentos fallidos y operaciones de
administración.

### ¿Cómo creo un proyecto nuevo?
Administración › Proyectos › **Proyectos y módulos**: cree el proyecto (código, nombre) y elija qué módulos tiene habilitados.

### ¿Por qué no puedo eliminar un proyecto?
No se puede eliminar la Empresa, el proyecto que tiene abierto ni el último proyecto activo. La eliminación pide escribir el
código exacto, marcar la casilla, esperar unos segundos y su contraseña; el proyecto queda en Eliminados y se puede restaurar.

## Relacionado
- [Uso general](general.md) — roles y permisos explicados al usuario.
- [Bot de Telegram](bot-telegram.md).
