using CampusBite.Application.Productos.Dtos;

namespace CampusBite.Application.Productos;

// JY-Sprint4 27/10/2026: casos de uso de productos. Quién actúa sale de IUsuarioActual.
public interface IProductoService
{
    // TEC-09: alta de producto. imagen es opcional; si viene, pasa por las mismas reglas que SubirImagenAsync
    Task<ProductoDto> CrearAsync(
        Guid idCafeteria, GuardarProductoDto dto, ArchivoImagen? imagen, CancellationToken cancellationToken = default);

    // TEC-10: cambia la imagen de un producto existente y borra la anterior
    Task<ImagenProductoDto> SubirImagenAsync(
        Guid idCafeteria, Guid idProducto, ArchivoImagen archivo, CancellationToken cancellationToken = default);
}
