using CampusBite.Application.Productos.Dtos;

namespace CampusBite.Application.Productos;

// JY-Sprint4 27/10/2026: casos de uso de productos. Quién actúa sale de IUsuarioActual.
public interface IProductoService
{
    // TEC-09: un producto por id. NotFoundException si no existe o el usuario no puede verlo
    Task<ProductoDto> ObtenerAsync(Guid idCafeteria, Guid idProducto, CancellationToken cancellationToken = default);

    // TEC-09: alta de producto; responde con el producto ya guardado (ObtenerAsync). imagen es opcional; si viene, pasa por las mismas reglas que SubirImagenAsync
    Task<ProductoDto> CrearAsync(
        Guid idCafeteria, GuardarProductoDto dto, ArchivoImagen? imagen, CancellationToken cancellationToken = default);

    // TEC-10: cambia la imagen de un producto existente y borra la anterior
    Task<ImagenProductoDto> SubirImagenAsync(
        Guid idCafeteria, Guid idProducto, ArchivoImagen archivo, CancellationToken cancellationToken = default);

    // JJ-Sprint4 27/10/2026: TEC-09. CRUD completo de productos y categorías
    Task<IReadOnlyList<ProductoDto>> ListarAsync(Guid idCafeteria, CancellationToken cancellationToken = default);

    Task ActualizarAsync(Guid idProducto, GuardarProductoDto dto, CancellationToken cancellationToken = default);

    Task ToggleDisponibleAsync(Guid idProducto, bool disponible, CancellationToken cancellationToken = default);

    Task EliminarAsync(Guid idProducto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(Guid idCafeteria, CancellationToken cancellationToken = default);

    Task<Guid> CrearCategoriaAsync(Guid idCafeteria, string nombre, CancellationToken cancellationToken = default);

    Task EliminarCategoriaAsync(Guid idCategoria, CancellationToken cancellationToken = default);
}
