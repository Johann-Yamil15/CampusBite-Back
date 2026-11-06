namespace CampusBite.Application.Productos;

// JY-Sprint4 27/10/2026: TEC-10. El archivo tal como llegó en la petición, sin tipos de ASP.NET (IFormFile se
// queda en la Api). Nada de esto es confiable todavía: ProductoService valida tipo, tamaño y contenido.
public record ArchivoImagen(Stream Contenido, string? ContentType, long Longitud);
