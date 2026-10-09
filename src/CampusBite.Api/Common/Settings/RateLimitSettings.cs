namespace CampusBite.Api.Common.Settings;

// JJ-Sprint2 09/10/2026: límite de intentos por IP para login y registro (variables RateLimit__*)
public class RateLimitSettings
{
    public const string SectionName = "RateLimit";
    public const string PoliticaAuth = "auth";

    // Peticiones permitidas por IP en cada ventana; alto a propósito porque en el campus muchos alumnos salen por la misma IP
    public int AuthPermitLimit { get; set; } = 20;
    public int AuthWindowSeconds { get; set; } = 60;
}
