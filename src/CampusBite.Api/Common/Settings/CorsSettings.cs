namespace CampusBite.Api.Common.Settings;

// JJ-Sprint2 09/10/2026: orígenes permitidos para CORS. Por ambiente en appsettings.json (producción) y
// appsettings.Development.json (local); la variable Cors__AllowedOrigins los reemplaza si se define
public class CorsSettings
{
    public const string SectionName = "Cors";
    public const string PolicyName = "CampusBiteCors";

    // Separados por coma. Vacío = cualquier origen
    public string AllowedOrigins { get; set; } = string.Empty;

    // El navegador manda el origen sin "/" final; se quita por si alguien la copia con la URL
    public string[] GetOrigins() => AllowedOrigins
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(origen => origen.TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
