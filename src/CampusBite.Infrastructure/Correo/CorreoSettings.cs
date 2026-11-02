namespace CampusBite.Infrastructure.Correo;

// JY-Sprint4 31/10/2026: servidor SMTP para los correos (sección "Correo", variables Correo__*). Sirve cualquier
// proveedor: Gmail con contraseña de aplicación, Outlook, Brevo, o Mailtrap/smtp4dev para probar en local.
public sealed class CorreoSettings
{
    public const string SectionName = "Correo";

    public string Host { get; set; } = string.Empty;
    public int Puerto { get; set; } = 587;                        // 587 = STARTTLS; 465 = TLS directo
    public string Usuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Remitente { get; set; } = string.Empty;         // si va vacío se usa Usuario
    public string NombreRemitente { get; set; } = "CampusBite";
    public int TiempoLimiteSegundos { get; set; } = 30;

    // Página del frontend que recibe ?token=... y llama a POST /api/auth/verificar-correo
    public string UrlVerificacion { get; set; } = string.Empty;
}
