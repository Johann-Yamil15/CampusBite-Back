namespace CampusBite.Application.Productos;

// JY-Sprint4 27/10/2026: TEC-10. Almacén de imágenes; Infrastructure lo implementa con Azure Blob Storage.
// Los reintentos por red lenta o caída momentánea los hace la implementación. Si aun así falla lanza
// ServiceUnavailableException (503): el usuario puede repetir la petición.
public interface IBlobService
{
    /// <summary>
    /// Sube el stream al contenedor y devuelve la URL pública permanente.
    /// El stream debe permitir Seek: si un intento se corta, se vuelve a mandar desde el inicio.
    /// </summary>
    Task<string> SubirImagenProductoAsync(
        Guid idProducto,
        Stream contenido,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina el blob si la URL pertenece a este contenedor.
    /// Silencioso si no existe o si la URL es de otro sitio (idempotente).
    /// </summary>
    Task EliminarImagenAsync(string urlBlob, CancellationToken ct = default);
}
