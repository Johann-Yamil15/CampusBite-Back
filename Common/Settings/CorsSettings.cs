namespace CampusBite_Back.Common.Settings;

// JJ-Sprint1 02/10/2026: orígenes permitidos para CORS (variable Cors__AllowedOrigins, separados por coma)
public class CorsSettings
{
    public const string SectionName = "Cors";
    public const string PolicyName = "CampusBiteCors";

    public string AllowedOrigins { get; set; } = string.Empty;

    public string[] GetOrigins() => AllowedOrigins
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
