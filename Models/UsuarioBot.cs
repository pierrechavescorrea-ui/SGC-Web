namespace SigWeb.Models;

// Persona autorizada a usar el bot de Telegram de SigWeb (Configuración › Bot Telegram).
// Se vincula con /vincular <código> (código de un solo uso generado en SigWeb); el bot
// solo responde a los chats que estén aquí y activos. Desactivar = deja de responderle
// (no se borra: queda la constancia de quién tuvo acceso).
public class UsuarioBot
{
    public int Id { get; set; }
    public long ChatId { get; set; }                 // chat privado de Telegram (único)
    public string Nombre { get; set; } = string.Empty;   // el que se escribió al generar el código
    public string? UsuarioTelegram { get; set; }     // @usuario y nombre visibles en Telegram al vincular
    public bool Activo { get; set; } = true;
    public DateTime FechaVinculo { get; set; } = DateTime.Now;
    public DateTime? UltimoUso { get; set; }
}
