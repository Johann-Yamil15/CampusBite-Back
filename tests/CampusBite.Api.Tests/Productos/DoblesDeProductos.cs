using CampusBite.Application.Productos;
using CampusBite.Application.Productos.Dtos;

namespace CampusBite.Api.Tests.Productos;

// JY-Sprint4 27/10/2026: TEC-09/TEC-10. Dobles de Azure (almacén) y de la BD (repositorio) para las pruebas de productos
internal sealed class AlmacenFalso : IBlobService
{
    public List<string> Subidas { get; } = [];
    public List<string> Eliminadas { get; } = [];
    public Exception? ErrorAlSubir { get; set; }

    public Task<string> SubirImagenProductoAsync(Guid idProducto, Stream contenido, string contentType, CancellationToken ct = default)
    {
        if (ErrorAlSubir is not null)
        {
            throw ErrorAlSubir;
        }

        var url = $"https://campusbite.blob.core.windows.net/productos/{idProducto}_{Subidas.Count}.png";
        Subidas.Add(url);
        return Task.FromResult(url);
    }

    public Task EliminarImagenAsync(string urlBlob, CancellationToken ct = default)
    {
        Eliminadas.Add(urlBlob);
        return Task.CompletedTask;
    }
}

internal sealed class RepositorioFalso : IProductoRepository
{
    public string? ImagenActual { get; set; }
    public Exception? Error { get; set; }
    public DatosProducto? UltimosDatos { get; private set; }
    public bool Eliminado { get; private set; }
    public bool? UltimoToggle { get; private set; }

    // Productos y categorías en memoria para que Listar devuelva algo
    public List<ProductoDto> Productos { get; } = [];
    public List<CategoriaDto> Categorias { get; } = [];

    public Task<ProductoDto> CrearAsync(
        Guid idProducto, Guid idCafeteria, Guid idActor, DatosProducto datos, string? imagenUrl,
        CancellationToken cancellationToken = default)
    {
        if (Error is not null) throw Error;

        UltimosDatos = datos;
        ImagenActual = imagenUrl;
        var producto = new ProductoDto
        {
            IdProducto = idProducto, IdCafeteria = idCafeteria, IdCategoria = datos.IdCategoria, Nombre = datos.Nombre,
            Descripcion = datos.Descripcion, Precio = datos.Precio, ImagenUrl = imagenUrl, Disponible = datos.Disponible,
            CreadoEn = DateTimeOffset.UtcNow, ActualizadoEn = DateTimeOffset.UtcNow
        };
        Productos.Add(producto);
        return Task.FromResult(producto);
    }

    public Task<string?> ActualizarImagenAsync(
        Guid idCafeteria, Guid idProducto, Guid idActor, string imagenUrl, CancellationToken cancellationToken = default)
    {
        if (Error is not null) throw Error;
        var anterior = ImagenActual;
        ImagenActual = imagenUrl;
        return Task.FromResult(anterior);
    }

    // JJ-Sprint4 27/10/2026: TEC-09
    public Task<IReadOnlyList<ProductoDto>> ListarAsync(
        Guid idCafeteria, Guid idActor, CancellationToken cancellationToken = default)
    {
        if (Error is not null) throw Error;
        return Task.FromResult<IReadOnlyList<ProductoDto>>(Productos);
    }

    public Task ActualizarAsync(
        Guid idProducto, Guid idActor, DatosProducto datos, CancellationToken cancellationToken = default)
    {
        if (Error is not null) throw Error;
        UltimosDatos = datos;
        return Task.CompletedTask;
    }

    public Task ToggleDisponibleAsync(
        Guid idProducto, bool disponible, Guid idActor, CancellationToken cancellationToken = default)
    {
        if (Error is not null) throw Error;
        UltimoToggle = disponible;
        return Task.CompletedTask;
    }

    public Task EliminarAsync(
        Guid idProducto, Guid idActor, CancellationToken cancellationToken = default)
    {
        if (Error is not null) throw Error;
        Eliminado = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(
        Guid idCafeteria, CancellationToken cancellationToken = default)
    {
        if (Error is not null) throw Error;
        return Task.FromResult<IReadOnlyList<CategoriaDto>>(Categorias);
    }

    public Task<Guid> CrearCategoriaAsync(
        Guid idCafeteria, Guid idActor, string nombre, CancellationToken cancellationToken = default)
    {
        if (Error is not null) throw Error;
        var id = Guid.NewGuid();
        Categorias.Add(new CategoriaDto(id, nombre));
        return Task.FromResult(id);
    }

    public Task EliminarCategoriaAsync(
        Guid idCategoria, Guid idActor, CancellationToken cancellationToken = default)
    {
        if (Error is not null) throw Error;
        Categorias.RemoveAll(c => c.IdCategoria == idCategoria);
        return Task.CompletedTask;
    }
}
