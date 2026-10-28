using CampusBite.Application.Productos.Dtos;

namespace CampusBite.Application.Productos;

// JY-Sprint4 27/10/2026: TEC-09/TEC-10. Casos de uso de productos. Quién actúa sale de IUsuarioActual.
public interface IProductoService
{
    // imagen es opcional; si viene, pasa por las mismas reglas que SubirImagenAsync
    Task<ProductoDto> CrearAsync(
        Guid idCafeteria, GuardarProductoDto dto, ArchivoImagen? imagen, CancellationToken cancellationToken = default);

    Task<ImagenProductoDto> SubirImagenAsync(
        Guid idCafeteria, Guid idProducto, ArchivoImagen archivo, CancellationToken cancellationToken = default);
}
