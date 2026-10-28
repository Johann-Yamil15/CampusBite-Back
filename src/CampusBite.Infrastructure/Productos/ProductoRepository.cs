using System.Text.Json;
using CampusBite.Application.Productos;
using CampusBite.Application.Productos.Dtos;
using CampusBite.Infrastructure.Persistence;
using static CampusBite.Infrastructure.Persistence.Procedimientos;

namespace CampusBite.Infrastructure.Productos;

// JY-Sprint4 27/10/2026: TEC-09/TEC-10. Implementa IProductoRepository con los procedimientos sp_*_producto.
// Los permisos (admin_sistema / encargado de esa cafetería) los valida la BD con el idActor.
// Los errores de la BD llegan ya traducidos a excepciones de Domain (Persistence/Procedimientos.cs).
public class ProductoRepository : IProductoRepository
{
    private readonly AppDbContext _context;

    public ProductoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ProductoDto> CrearAsync(
        Guid idProducto, Guid idCafeteria, Guid idActor, DatosProducto datos, string? imagenUrl,
        CancellationToken cancellationToken = default) =>
        await _context.EjecutarAsync(
            "CALL sp_crear_producto(@id, @cafeteria, @actor, @categoria, @nombre, @descripcion, @precio, @disponible, @url, NULL)",
            [
                Id("id", idProducto), Id("cafeteria", idCafeteria), Id("actor", idActor),
                IdOpcional("categoria", datos.IdCategoria), Texto("nombre", datos.Nombre),
                Texto("descripcion", datos.Descripcion), Numero("precio", datos.Precio),
                Logico("disponible", datos.Disponible), Texto("url", imagenUrl)
            ],
            fila => fila.LeerJson<ProductoDto>(0),
            cancellationToken)
        ?? throw new InvalidOperationException("sp_crear_producto no devolvió el producto creado.");

    public Task<string?> ActualizarImagenAsync(
        Guid idCafeteria, Guid idProducto, Guid idActor, string imagenUrl, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync<string?>(
            "CALL sp_actualizar_imagen_producto(@cafeteria, @producto, @actor, @url, NULL)",
            [Id("cafeteria", idCafeteria), Id("producto", idProducto), Id("actor", idActor), Texto("url", imagenUrl)],
            fila => fila.IsDBNull(0) ? null : fila.GetString(0),
            cancellationToken);

    // JJ-Sprint4 27/10/2026: TEC-09. CRUD completo de productos y categorías

    public async Task<IReadOnlyList<ProductoDto>> ListarAsync(
        Guid idCafeteria, Guid idActor, CancellationToken cancellationToken = default)
    {
        var json = await _context.EjecutarAsync(
            "CALL sp_listar_productos_cafeteria(@cafeteria, @actor, NULL)",
            [Id("cafeteria", idCafeteria), Id("actor", idActor)],
            fila => fila.GetString(0),
            cancellationToken);
        return JsonSerializer.Deserialize<List<ProductoDto>>(json, Json) ?? [];
    }

    public Task ActualizarAsync(
        Guid idProducto, Guid idActor, DatosProducto datos, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_actualizar_producto(@producto, @actor, @categoria, @nombre, @descripcion, @precio, @disponible)",
            [
                Id("producto", idProducto), Id("actor", idActor), IdOpcional("categoria", datos.IdCategoria),
                Texto("nombre", datos.Nombre), Texto("descripcion", datos.Descripcion),
                Numero("precio", datos.Precio), Logico("disponible", datos.Disponible)
            ],
            cancellationToken);

    public Task ToggleDisponibleAsync(
        Guid idProducto, bool disponible, Guid idActor, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_toggle_disponible_producto(@producto, @disponible, @actor)",
            [Id("producto", idProducto), Logico("disponible", disponible), Id("actor", idActor)],
            cancellationToken);

    public Task EliminarAsync(
        Guid idProducto, Guid idActor, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_eliminar_producto(@producto, @actor)",
            [Id("producto", idProducto), Id("actor", idActor)],
            cancellationToken);

    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(
        Guid idCafeteria, CancellationToken cancellationToken = default)
    {
        var json = await _context.EjecutarAsync(
            "CALL sp_listar_categorias(@cafeteria, NULL)",
            [Id("cafeteria", idCafeteria)],
            fila => fila.GetString(0),
            cancellationToken);
        return JsonSerializer.Deserialize<List<CategoriaDto>>(json, Json) ?? [];
    }

    public Task<Guid> CrearCategoriaAsync(
        Guid idCafeteria, Guid idActor, string nombre, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_crear_categoria(@cafeteria, @actor, @nombre, NULL)",
            [Id("cafeteria", idCafeteria), Id("actor", idActor), Texto("nombre", nombre)],
            fila => fila.GetGuid(0),
            cancellationToken);

    public Task EliminarCategoriaAsync(
        Guid idCategoria, Guid idActor, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_eliminar_categoria(@categoria, @actor)",
            [Id("categoria", idCategoria), Id("actor", idActor)],
            cancellationToken);
}
