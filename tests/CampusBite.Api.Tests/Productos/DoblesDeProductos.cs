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

    // Lo que "hay en la BD", por (cafetería, producto); el alta agrega aquí
    public Dictionary<(Guid IdCafeteria, Guid IdProducto), ProductoDto> Productos { get; } = [];

    public Task<ProductoDto?> ObtenerAsync(
        Guid idCafeteria, Guid idProducto, Guid idActor, CancellationToken cancellationToken = default) =>
        Task.FromResult(Productos.GetValueOrDefault((idCafeteria, idProducto)));

    public Task CrearAsync(
        Guid idProducto, Guid idCafeteria, Guid idActor, DatosProducto datos, string? imagenUrl,
        CancellationToken cancellationToken = default)
    {
        if (Error is not null)
        {
            throw Error;
        }

        UltimosDatos = datos;
        ImagenActual = imagenUrl;
        Productos[(idCafeteria, idProducto)] = new ProductoDto
        {
            IdProducto = idProducto, IdCafeteria = idCafeteria, IdCategoria = datos.IdCategoria, Nombre = datos.Nombre,
            Descripcion = datos.Descripcion, Precio = datos.Precio, ImagenUrl = imagenUrl, Disponible = datos.Disponible,
            CreadoEn = DateTimeOffset.UtcNow, ActualizadoEn = DateTimeOffset.UtcNow
        };
        return Task.CompletedTask;
    }

    public Task<string?> ActualizarImagenAsync(
        Guid idCafeteria, Guid idProducto, Guid idActor, string imagenUrl, CancellationToken cancellationToken = default)
    {
        if (Error is not null)
        {
            throw Error;
        }

        var anterior = ImagenActual;
        ImagenActual = imagenUrl;
        return Task.FromResult(anterior);
    }
}
