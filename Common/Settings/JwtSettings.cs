namespace CampusBite_Back.Common.Settings;

// JJ-Sprint1 02/10/2026: configuración de JWT enlazada desde la sección "Jwt" (variables Jwt__*)
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
