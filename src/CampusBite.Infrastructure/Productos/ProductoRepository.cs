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
}
