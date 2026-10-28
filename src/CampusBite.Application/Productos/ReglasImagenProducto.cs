using CampusBite.Domain.Common.Exceptions;

namespace CampusBite.Application.Productos;

// JY-Sprint4 27/10/2026: TEC-10. Qué imagen se acepta para un producto. Única fuente de las reglas: la Api usa
// TamanoMaximoBytes para cortar el cuerpo antes de leerlo y el servicio vuelve a revisar todo con los bytes reales.
// No se confía en lo que manda el cliente: Content-Type y longitud se pueden falsear, la firma del archivo no.
public static class ReglasImagenProducto
{
    public const long TamanoMaximoBytes = 5 * 1024 * 1024;   // 5 MB
    private const string TamanoMaximoTexto = "5 MB";

    // Content-Type aceptado -> con el que se guarda. "image/jpg" no es estándar pero lo mandan algunos celulares
    private static readonly Dictionary<string, string> TiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "image/jpeg",
        ["image/jpg"] = "image/jpeg",
        ["image/png"] = "image/png",
        ["image/webp"] = "image/webp"
    };

    // Primeros bytes de cada formato: JPEG FF D8 FF; PNG 89 'PNG' 0D 0A 1A 0A; WebP 'RIFF' <tamaño> 'WEBP'
    private static readonly byte[] FirmaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] FirmaRiff = "RIFF"u8.ToArray();
    private static readonly byte[] FirmaWebp = "WEBP"u8.ToArray();
    private const int BytesDeFirma = 12;

    // Devuelve el Content-Type normalizado ("image/png"); ignora parámetros como "; charset=..."
    public static string ValidarTipo(string? contentType)
    {
        var tipo = contentType?.Split(';', 2)[0].Trim() ?? string.Empty;
        return TiposPermitidos.TryGetValue(tipo, out var normalizado)
            ? normalizado
            : throw new BusinessRuleException("Solo se aceptan imágenes JPEG, PNG o WebP.");
    }

    public static void ValidarTamano(long bytes)
    {
        if (bytes <= 0)
        {
            throw new BusinessRuleException("El archivo está vacío.");
        }

        if (bytes > TamanoMaximoBytes)
        {
            throw ExcedeTamano();
        }
    }

    // Copia el archivo a memoria (máx. 5 MB) contando los bytes reales, no la longitud que dijo el cliente.
    // En memoria el stream permite Seek: si la subida a Azure se corta, el reintento lo vuelve a mandar completo.
    public static async Task<MemoryStream> LeerAsync(Stream origen, CancellationToken cancellationToken)
    {
        var copia = new MemoryStream();
        var buffer = new byte[81_920];
        int leidos;
        while ((leidos = await origen.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (copia.Length + leidos > TamanoMaximoBytes)
            {
                await copia.DisposeAsync();
                throw ExcedeTamano();
            }

            copia.Write(buffer, 0, leidos);
        }

        ValidarTamano(copia.Length);
        copia.Position = 0;
        return copia;
    }

    // El contenido debe ser de verdad el formato que dice el Content-Type (evita subir un .exe o un HTML
    // renombrado como imagen, que el contenedor público serviría tal cual)
    public static void ValidarContenido(ReadOnlySpan<byte> contenido, string contentType)
    {
        var cabecera = contenido[..Math.Min(contenido.Length, BytesDeFirma)];
        var coincide = contentType switch
        {
            "image/jpeg" => cabecera.StartsWith(FirmaJpeg),
            "image/png" => cabecera.StartsWith(FirmaPng),
            "image/webp" => cabecera.Length == BytesDeFirma && cabecera.StartsWith(FirmaRiff) && cabecera[8..].SequenceEqual(FirmaWebp),
            _ => false
        };

        if (!coincide)
        {
            throw new BusinessRuleException("El archivo no es una imagen válida o no coincide con su tipo (JPEG, PNG o WebP).");
        }
    }

    private static BusinessRuleException ExcedeTamano() =>
        new($"La imagen pesa más de {TamanoMaximoTexto}. Redúcela e intenta de nuevo.");
}
