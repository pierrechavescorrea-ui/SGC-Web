---
modulo: administracion
titulo: Bot de Telegram
menu: Administración › Bot de Telegram
rutas: [/admin/telegram, /bot-telegram]
palabras: [telegram, bot, celular, teléfono, vincular, código, QR, /torre, /buscar, /planos, /documentos, /equipo, /estados, /consecutivo, consultar desde el celular, chat privado, código de vinculación, cambiar de proyecto, usar bot, chat desactivado, chat ID]
opciones:
  - Bot de Telegram: /admin/telegram
  - Generar código: /admin/telegram | Generar código
  - Anular código: /admin/telegram | Anular código
  - Copiar enlace: /admin/telegram | Copiar enlace
  - Descargar QR (PNG): /admin/telegram | Descargar QR (PNG)
  - Ampliar QR: /admin/telegram | Ampliar QR
  - Editar vínculo: /admin/telegram | Editar vínculo
  - Reiniciar activación: /admin/telegram | Reiniciar activación
  - Pasar al registro central: /admin/telegram | Pasar al registro central
  - Eliminar vínculo: /admin/telegram | Eliminar vínculo
  - Actualizar: /admin/telegram | Actualizar
  - Buscar chat: /admin/telegram | Buscar chat
  - Filtrar por proyecto: /admin/telegram | Filtrar por proyecto
  - Reducir QR: /admin/telegram | Reducir QR
  - Guardar cambios: /admin/telegram | Guardar cambios
  - Desactivar chat: /admin/telegram | Desactivar chat
  - Activar chat: /admin/telegram | Activar chat
  - Usuario de SGC Web: /admin/telegram | Usuario de SGC Web
  - Nombre del chat: /admin/telegram | Nombre del chat
  - Proyecto: /admin/telegram | Proyecto
  - Cancelar: /admin/telegram | Cancelar
  - Confirmar eliminación: /admin/telegram | Confirmar eliminación
  - Generar código de vinculación: /mi-cuenta | Generar código de vinculación
actualizado: 2026-11-21
---

# Bot de Telegram

## Para qué sirve
Permite consultar SGC Web **desde el celular** con Telegram: hoja de vida de una torre, planos, documentos,
calibración de equipos, estado de las torres y avance, y pedir un **consecutivo** de comunicado.
El bot solo responde a chats **vinculados** y respeta los permisos del usuario.

## Cómo entrar
- Para vincular su propio chat: **Mi cuenta** › **Bot de Telegram** › **Generar código de vinculación**.
- Para administrar los chats vinculados: **Administración** › **Bot de Telegram**.

## Cómo hacer…

### Cómo vincular su Telegram
1. En SGC Web, genere el código (**Mi cuenta** › **Bot de Telegram**, o **Generar código** en Administración › Bot de Telegram).
   El código tiene 6 dígitos, dura **15 minutos** y sirve una sola vez.
2. Abra el bot en Telegram (con el enlace, **Copiar enlace**, o escaneando el QR, **Descargar QR (PNG)**).
3. Envíe: `/vincular 123456` (con su código).
4. El bot responde «Listo… este chat quedó vinculado con SGC Web».

Solo funciona en chats privados (no en grupos).

### Qué puede pedirle al bot
| Comando | Qué hace |
|---|---|
| `/torre 45` (o solo `45`) | Hoja de vida de la torre: avance, PIE, archivos, ensayos, materiales, riesgos, planes, actividades |
| `/buscar texto` (o cualquier texto) | Busca en todo SGC Web |
| `/planos` · `/plano código` | Planos por grupo · un plano |
| `/documentos` · `/doc código` | Documentos por tipología · un documento |
| `/equipos` · `/equipo nombre, marca o serie` | Equipos por categoría · estado de calibración de un equipo |
| `/estados` (también `/estado` o `/avance`) | Estado de las torres y avance de obra, tiro por tiro |
| `/consecutivo` | Pedir un consecutivo de comunicado, paso a paso |
| `/misconsecutivos` | Confirmar el envío o anular sus consecutivos |
| `/proyecto` | Cambiar de proyecto |
| `/cancelar` | Cancelar la conversación en curso |
| `/ayuda` (también `/start` o `/help`) | Lista de comandos |

**Lista mientras escribe:** en la caja de texto escriba `@nombre_del_bot 57` (torres), `@… p cim` (planos),
`@… d PR` (documentos) o `@… e estación` (equipos).

### Cómo administrar los chats vinculados (Administración › Bot de Telegram)
Desde el módulo consulte el estado de conexión y la URL pública de SGC Web. Use **Compartir el bot** para copiar el enlace, ampliar el QR o descargarlo en PNG.

Para **Generar código**, elija un proyecto activo. Puede asociarlo a un usuario de SGC Web o escribir el nombre del chat si la configuración admite chats sin usuario. **Códigos pendientes** reúne los códigos sin usar de todos los proyectos. **Anular código** invalida el seleccionado.

En **Chats vinculados** puede filtrar por proyecto y buscar por chat o usuario:

- **Editar vínculo** permite cambiar el nombre, el usuario de SGC Web y el proyecto. Use **Guardar cambios** para aplicar la edición.
- **Reiniciar activación** activa el chat y actualiza su fecha de vinculación.
- **Activar chat** y **Desactivar chat** cambian el acceso sin borrar el registro.
- **Eliminar vínculo** pide confirmación en la fila. El chat deberá vincularse otra vez.
- **Pasar al registro central** registra un chat marcado **Anterior** en el proyecto donde se encontró. Al asignarle un usuario o moverlo de proyecto, también pasa al registro central.

### Cómo cambiar el proyecto o consultar la ayuda desde Telegram

1. En el chat privado envíe `/proyecto`.
2. Seleccione uno de sus proyectos habilitados para Telegram.
3. Envíe `/ayuda` para ver los comandos. Si estaba solicitando un consecutivo, puede salir de ese flujo con `/cancelar`.

### Cómo retirar el acceso a un chat

1. Abra **Bot de Telegram** en Administración y use **Buscar chat** o **Filtrar por proyecto**.
2. Use **Desactivar chat** para conservar el vínculo e impedir su uso. **Activar chat** vuelve a habilitarlo.
3. Si quiere retirar el vínculo, toque **Eliminar vínculo** y **Confirmar eliminación**. El chat deberá vincularse de nuevo.

## Permisos
Para usar el bot hace falta el permiso **Consultas por Telegram**; cada consulta respeta además el permiso del módulo.
Los roles de fábrica incluyen **Ver** en Consultas por Telegram. **Consulta sin descarga** tiene solo **Ver** en ese módulo. El **Administrador del SGC** gestiona los vínculos en **Administración › Bot de Telegram**. En **Mi cuenta › Bot de Telegram**, use **Generar código de vinculación**. La app pide activar la verificación en dos pasos si la configuración la exige para su cuenta.

## Problemas frecuentes
- **«El código no funciona»** → venció (15 minutos) o ya se usó. Genere otro. Envíelo así: `/vincular 123456`.
- **«El bot dice que no tengo permiso»** → su rol no ve ese módulo en el proyecto activo. Cambie con `/proyecto` o pida acceso.
- **«El bot dice: No conozco ese comando»** → envíe `/ayuda` para ver los comandos.
- **«Los botones Abrir en SGC Web no aparecen»** → la app no tiene configurada una dirección pública. Avise al administrador.

## Preguntas frecuentes

### ¿Cómo vinculo mi Telegram?
Entre a **Mi cuenta**, genere su código y envíelo al bot como `/vincular 123456`. El código dura 15 minutos y se usa una vez.
Desde ese momento el bot le responde con sus mismos permisos.

### ¿Dónde administro los chats vinculados?
Solo en Administración › **Bot de Telegram**: estado del bot, enlace y QR para compartirlo, códigos de vinculación y chats de
todos los proyectos (activar, desactivar, renombrar, cambiar usuario o proyecto, eliminar).

### ¿Por qué el bot no me responde?
Revise que su chat esté activo en Administración › Bot de Telegram, que el código no haya vencido y que su rol tenga el
permiso «Consultas por Telegram» en el proyecto. Si el estado dice «Sin token», el bot no está configurado en el servidor.

### ¿Qué le puedo preguntar al bot?
Torres (`/torre 143`), planos (`/plano`), documentos (`/doc`), equipos (`/equipo`), búsqueda libre (`/buscar` o texto suelto),
estado de las torres (`/estados`) y consecutivos de comunicados (`/consecutivo`).

### ¿Puedo usar el bot en un grupo?
No. El bot solo atiende chats privados.

### ¿Qué pasó con Configuración › Bot Telegram?
Se unificó en Administración › Bot de Telegram. El enlace anterior lleva automáticamente al lugar nuevo.

### ¿Cómo comparto el bot con alguien de obra?
En Administración › Bot de Telegram copie el enlace o descargue el QR. Para usarlo, la persona necesita además un código de
vinculación.

### Me salió «demasiados intentos» al vincular, ¿qué hago?
Tras 5 códigos fallidos en una hora, el bot bloquea ese chat por un rato. Espere y pida un código nuevo.

### ¿Cómo cambio de proyecto sin volver a vincular mi Telegram?
Envíe `/proyecto` y elija un proyecto al que tenga acceso para consultas por Telegram.

### ¿Cómo consulto mis consecutivos o anulo uno sin usar?
Envíe `/misconsecutivos` y elija el consecutivo y la acción disponible.

### ¿Qué comando uso para ver equipos por categoría?
Envíe `/equipos` sin argumento. Para un equipo concreto, envíe `/equipo` seguido por su nombre, marca o serie.

### ¿Cómo quito un código que envié a la persona equivocada?

Busque el código en Códigos pendientes y toque **Anular código**.

## Relacionado
- [Comunicados](comunicados.md) — consecutivos.
- [Trazado](trazado.md) — hoja de vida de la torre.
- [Mi cuenta](mi-cuenta.md)
