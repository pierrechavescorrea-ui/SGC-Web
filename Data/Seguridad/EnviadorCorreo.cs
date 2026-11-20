using System.Collections.Concurrent;
using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SigWeb.Data.Seguridad;

// Correo saliente (invitaciones, restablecer contraseña, avisos de seguridad).
//   Correo:Modo = "Smtp" → servidor SMTP (Amazon SES por su interfaz SMTP, Office 365, etc.).
//   Correo:Modo = "Archivo" (por defecto en desarrollo) → .eml en App_Data/correos y lista en Administración › Sistema.
// Credenciales SOLO en user-secrets / variables de entorno: Correo__Smtp__Usuario, Correo__Smtp__Clave.
public sealed class EnviadorCorreo
{
    private readonly IConfiguration _cfg;
    private readonly IHostEnvironment _env;
    private readonly ILogger<EnviadorCorreo> _log;
    private readonly ConcurrentQueue<CorreoPrueba> _recientes = new();

    public sealed record CorreoPrueba(DateTime Fecha, string Para, string Asunto, string Html);

    public EnviadorCorreo(IConfiguration cfg, IHostEnvironment env, ILogger<EnviadorCorreo> log) { _cfg = cfg; _env = env; _log = log; }

    public string Modo => _cfg["Correo:Modo"] ?? (_env.IsDevelopment() ? "Archivo" : "Smtp");
    public bool Configurado => Modo.Equals("Archivo", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(_cfg["Correo:Smtp:Servidor"]);
    public string Remitente => _cfg["Correo:Remitente"] ?? "SGC Web <no-responder@localhost>";
    public IReadOnlyList<CorreoPrueba> Recientes => _recientes.Reverse().ToList();

    public async Task<bool> EnviarAsync(string para, string asunto, string html, CancellationToken ct = default)
    {
        try
        {
            var m = new MimeMessage();
            m.From.Add(MailboxAddress.Parse(Remitente));
            m.To.Add(MailboxAddress.Parse(para));
            m.Subject = asunto;
            m.Body = new BodyBuilder { HtmlBody = html, TextBody = ATexto(html) }.ToMessageBody();

            if (Modo.Equals("Archivo", StringComparison.OrdinalIgnoreCase))
            {
                var carpeta = Path.Combine(_env.ContentRootPath, "App_Data", "correos");
                Directory.CreateDirectory(carpeta);
                await m.WriteToAsync(Path.Combine(carpeta, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml"), ct);
                _recientes.Enqueue(new CorreoPrueba(DateTime.Now, para, asunto, html));
                while (_recientes.Count > 30) _recientes.TryDequeue(out _);
                _log.LogInformation("Correo (modo archivo) para {Para}: {Asunto}", para, asunto);
                return true;
            }

            var servidor = _cfg["Correo:Smtp:Servidor"];
            if (string.IsNullOrWhiteSpace(servidor)) { _log.LogError("Correo sin configurar (Correo:Smtp:Servidor)."); return false; }
            using var smtp = new SmtpClient { Timeout = 20000 };
            var puerto = _cfg.GetValue("Correo:Smtp:Puerto", 587);
            var seguridad = puerto == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await smtp.ConnectAsync(servidor, puerto, seguridad, ct);
            if (!string.IsNullOrWhiteSpace(_cfg["Correo:Smtp:Usuario"]))
                await smtp.AuthenticateAsync(_cfg["Correo:Smtp:Usuario"], _cfg["Correo:Smtp:Clave"], ct);
            await smtp.SendAsync(m, ct);
            await smtp.DisconnectAsync(true, ct);
            return true;
        }
        catch (Exception ex)
        {
            // Sin el destinatario completo en el registro (dato personal): solo el dominio.
            var dominio = para.Contains('@') ? para[(para.IndexOf('@') + 1)..] : "?";
            _log.LogError(ex, "No se pudo enviar el correo «{Asunto}» (dominio {Dominio})", asunto, dominio);
            return false;
        }
    }

    private static string ATexto(string html) =>
        WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html.Replace("<br>", "\n").Replace("</p>", "\n\n"), "<[^>]+>", "")).Trim();

    // Plantilla institucional sobria (tabla simple: se ve igual en Outlook, Gmail y celulares).
    public static string Plantilla(string titulo, string cuerpoHtml, string? botonTexto = null, string? botonUrl = null, string? pie = null)
    {
        Func<string?, string> e = s => WebUtility.HtmlEncode(s ?? "");
        var boton = botonUrl is null ? "" :
            $"<tr><td style=\"padding:8px 32px 28px\"><a href=\"{e(botonUrl)}\" style=\"display:inline-block;background:#EC1C24;color:#fff;text-decoration:none;font-weight:600;padding:12px 26px;border-radius:8px\">{e(botonTexto ?? "Abrir")}</a></td></tr>" +
            $"<tr><td style=\"padding:0 32px 24px;color:#6b7280;font-size:12px\">Si el botón no funciona, copie este enlace en el navegador:<br><span style=\"word-break:break-all;color:#374151\">{e(botonUrl)}</span></td></tr>";
        return $"""
<!doctype html><html lang="es"><body style="margin:0;background:#f3f4f6;font-family:Segoe UI,Arial,sans-serif;color:#111827">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f3f4f6;padding:28px 0"><tr><td align="center">
<table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;width:100%;background:#fff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb">
<tr><td style="height:4px;background:#EC1C24"></td></tr>
<tr><td style="padding:26px 32px 6px;font-size:13px;letter-spacing:.14em;text-transform:uppercase;color:#6b7280">SGC Web · Sistema de gestión de calidad</td></tr>
<tr><td style="padding:4px 32px 12px;font-size:22px;font-weight:600">{e(titulo)}</td></tr>
<tr><td style="padding:0 32px 18px;font-size:15px;line-height:1.55;color:#374151">{cuerpoHtml}</td></tr>
{boton}
<tr><td style="padding:16px 32px 24px;border-top:1px solid #f0f0f0;color:#9ca3af;font-size:12px;line-height:1.5">{pie ?? "Mensaje automático: no responda a este correo. Si no esperaba este mensaje, ignórelo."}</td></tr>
</table></td></tr></table></body></html>
""";
    }
}
