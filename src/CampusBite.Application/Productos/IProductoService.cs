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

    // JJ-Sprint4 27/10/2026: TEC-09. CRUD completo de productos y categorías
    Task<IReadOnlyList<ProductoDto>> ListarAsync(Guid idCafeteria, CancellationToken cancellationToken = default);

    Task ActualizarAsync(Guid idProducto, GuardarProductoDto dto, CancellationToken cancellationToken = default);

    Task ToggleDisponibleAsync(Guid idProducto, bool disponible, CancellationToken cancellationToken = default);

    Task EliminarAsync(Guid idProducto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(Guid idCafeteria, CancellationToken cancellationToken = default);

    Task<Guid> CrearCategoriaAsync(Guid idCafeteria, string nombre, CancellationToken cancellationToken = default);

    Task EliminarCategoriaAsync(Guid idCategoria, CancellationToken cancellationToken = default);
}
