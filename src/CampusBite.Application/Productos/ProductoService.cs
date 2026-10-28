using CampusBite.Application.Common.Security;
using CampusBite.Application.Productos.Dtos;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace CampusBite.Application.Productos;

// JY-Sprint4 27/10/2026: TEC-09 (alta) y TEC-10 (imagen). Reglas de productos. Los permisos finos (¿es encargado de ESTA cafetería?)
// los revisa la BD; aquí se limpia la entrada y se maneja la imagen.
// Toda imagen (al crear o al cambiarla) sigue el mismo camino: validar -> subir -> guardar en la BD. Si la BD
// rechaza (permiso, nombre repetido, categoría ajena), se borra la imagen recién subida para no dejar archivos
// huérfanos. Repetir la petición por mala señal es seguro: un alta repetida choca con el nombre (409) y su imagen
// se borra; un cambio de imagen repetido deja una sola imagen y borra la del intento anterior.
public class ProductoService : IProductoService
{
    private readonly IProductoRepository _productos;
    private readonly IBlobService _blobs;
    private readonly IUsuarioActual _usuario;
    private readonly ILogger<ProductoService> _logger;

    public ProductoService(
        IProductoRepository productos, IBlobService blobs, IUsuarioActual usuario, ILogger<ProductoService> logger)
    {
        _productos = productos;
        _blobs = blobs;
        _usuario = usuario;
        _logger = logger;
    }

    public async Task<ProductoDto> ObtenerAsync(Guid idCafeteria, Guid idProducto, CancellationToken cancellationToken = default)
    {
        return await _productos.ObtenerAsync(idCafeteria, idProducto, _usuario.Id, cancellationToken)
               ?? throw new NotFoundException("El producto no existe.");
    }

    public async Task<ProductoDto> CrearAsync(
        Guid idCafeteria, GuardarProductoDto dto, ArchivoImagen? imagen, CancellationToken cancellationToken = default)
    {
        var idActor = _usuario.Id;
        var datos = Normalizar(dto);
        var idProducto = Guid.NewGuid();

        if (imagen is null)
        {
            await _productos.CrearAsync(idProducto, idCafeteria, idActor, datos, null, cancellationToken);
        }
        else
        {
            var url = await SubirAsync(idProducto, imagen, cancellationToken);
            await GuardarOBorrarAsync(url,
                () => _productos.CrearAsync(idProducto, idCafeteria, idActor, datos, url, cancellationToken));
        }

        return await ObtenerAsync(idCafeteria, idProducto, cancellationToken);
    }

    public async Task<ImagenProductoDto> SubirImagenAsync(
        Guid idCafeteria, Guid idProducto, ArchivoImagen archivo, CancellationToken cancellationToken = default)
    {
        var idActor = _usuario.Id;
        var url = await SubirAsync(idProducto, archivo, cancellationToken);

        string? anterior = null;
        await GuardarOBorrarAsync(url,
            async () => anterior = await _productos.ActualizarImagenAsync(idCafeteria, idProducto, idActor, url, cancellationToken));

        if (anterior is not null && anterior != url)
        {
            await EliminarSinFallarAsync(anterior, "fue reemplazada por una nueva");
        }

        return new ImagenProductoDto { ImagenUrl = url };
    }

    // Valida (lo barato primero: no se lee el archivo si el tipo o el tamaño declarado ya no sirven) y sube
    private async Task<string> SubirAsync(Guid idProducto, ArchivoImagen archivo, CancellationToken cancellationToken)
    {
        var contentType = ReglasImagenProducto.ValidarTipo(archivo.ContentType);
        ReglasImagenProducto.ValidarTamano(archivo.Longitud);

        await using var contenido = await ReglasImagenProducto.LeerAsync(archivo.Contenido, cancellationToken);
        ReglasImagenProducto.ValidarContenido(contenido.GetBuffer().AsSpan(0, (int)contenido.Length), contentType);

        return await _blobs.SubirImagenProductoAsync(idProducto, contenido, contentType, cancellationToken);
    }

    // Guarda en la BD; si falla, la imagen recién subida ya no la usa nadie y se borra
    private async Task GuardarOBorrarAsync(string urlNueva, Func<Task> guardar)
    {
        try
        {
            await guardar();
        }
        catch
        {
            await EliminarSinFallarAsync(urlNueva, "la BD rechazó el guardado");
            throw;
        }
    }

    // Limpieza de un blob que ya no se usa. Si Azure no responde, el blob queda huérfano (no afecta al usuario);
    // no tiene caso fallar una operación que ya quedó guardada, pero se deja un warning con la URL y el motivo
    // para poder encontrar y borrar los huérfanos si llegan a costar.
    // Sin CancellationToken: si el cliente se desconectó, la limpieza igual debe terminar.
    private async Task EliminarSinFallarAsync(string url, string motivo)
    {
        try
        {
            await _blobs.EliminarImagenAsync(url, CancellationToken.None);
        }
        catch (ServiceUnavailableException ex)
        {
            _logger.LogWarning(ex,
                "Imagen huérfana en Azure Blob Storage: no se pudo borrar {UrlImagen} ({Motivo})", url, motivo);
        }
    }

    // Recorta espacios; una descripción en blanco se guarda como null
    private static DatosProducto Normalizar(GuardarProductoDto dto) => new(
        dto.Nombre.Trim(),
        string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim(),
        dto.Precio!.Value,
        dto.IdCategoria,
        dto.Disponible);
}
