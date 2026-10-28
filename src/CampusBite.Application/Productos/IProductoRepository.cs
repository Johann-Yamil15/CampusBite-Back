using CampusBite.Application.Productos.Dtos;

namespace CampusBite.Application.Productos;

// JJ-Sprint4 27/10/2026: TEC-09 agrega CRUD completo de productos y categorías al contrato iniciado por TEC-10

// JY-Sprint4 27/10/2026: TEC-09/TEC-10. Acceso a productos; Infrastructure lo implementa con los sp_*_producto.
// idActor siempre es el usuario de la sesión (IUsuarioActual): la BD valida con él los permisos.
// Errores: NotFoundException (no existe o no es de esa cafetería), ForbiddenException, ConflictException
// (nombre repetido) y BusinessRuleException (datos inválidos).
public interface IProductoRepository
{
    // idProducto lo genera el servicio: la imagen se sube antes del alta y su nombre lleva ese id
    Task<ProductoDto> CrearAsync(
        Guid idProducto, Guid idCafeteria, Guid idActor, DatosProducto datos, string? imagenUrl,
        CancellationToken cancellationToken = default);

    // Guarda la URL nueva y devuelve la que tenía antes (null si no tenía), para borrar ese blob
    Task<string?> ActualizarImagenAsync(
        Guid idCafeteria, Guid idProducto, Guid idActor, string imagenUrl, CancellationToken cancellationToken = default);

    // JJ-Sprint4 27/10/2026: TEC-09. CRUD completo de productos y categorías
    Task<IReadOnlyList<ProductoDto>> ListarAsync(Guid idCafeteria, Guid idActor, CancellationToken cancellationToken = default);

    Task ActualizarAsync(Guid idProducto, Guid idActor, DatosProducto datos, CancellationToken cancellationToken = default);

    Task ToggleDisponibleAsync(Guid idProducto, bool disponible, Guid idActor, CancellationToken cancellationToken = default);

    Task EliminarAsync(Guid idProducto, Guid idActor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(Guid idCafeteria, CancellationToken cancellationToken = default);

    Task<Guid> CrearCategoriaAsync(Guid idCafeteria, Guid idActor, string nombre, CancellationToken cancellationToken = default);

    Task EliminarCategoriaAsync(Guid idCategoria, Guid idActor, CancellationToken cancellationToken = default);
}
