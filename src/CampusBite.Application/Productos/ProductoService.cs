using CampusBite.Application.Common.Security;
using CampusBite.Application.Productos.Dtos;
using CampusBite.Domain.Common.Exceptions;

namespace CampusBite.Application.Productos;

// JY-Sprint4 27/10/2026: TEC-09/TEC-10. Reglas de productos. Los permisos finos (¿es encargado de ESTA cafetería?)
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

    public ProductoService(IProductoRepository productos, IBlobService blobs, IUsuarioActual usuario)
    {
        _productos = productos;
        _blobs = blobs;
        _usuario = usuario;
    }

    public async Task<ProductoDto> CrearAsync(
        Guid idCafeteria, GuardarProductoDto dto, ArchivoImagen? imagen, CancellationToken cancellationToken = default)
    {
        var idActor = _usuario.Id;
        var datos = Normalizar(dto);
        var idProducto = Guid.NewGuid();

        if (imagen is null)
        {
            return await _productos.CrearAsync(idProducto, idCafeteria, idActor, datos, null, cancellationToken);
        }

        var url = await SubirAsync(idProducto, imagen, cancellationToken);
        return await GuardarOBorrarAsync(url,
            () => _productos.CrearAsync(idProducto, idCafeteria, idActor, datos, url, cancellationToken));
    }

    public async Task<ImagenProductoDto> SubirImagenAsync(
        Guid idCafeteria, Guid idProducto, ArchivoImagen archivo, CancellationToken cancellationToken = default)
    {
        var idActor = _usuario.Id;
        var url = await SubirAsync(idProducto, archivo, cancellationToken);

        var anterior = await GuardarOBorrarAsync(url,
            () => _productos.ActualizarImagenAsync(idCafeteria, idProducto, idActor, url, cancellationToken));

        if (anterior is not null && anterior != url)
        {
            await EliminarSinFallarAsync(anterior);
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
    private async Task<T> GuardarOBorrarAsync<T>(string urlNueva, Func<Task<T>> guardar)
    {
        try
        {
            return await guardar();
        }
        catch
        {
            await EliminarSinFallarAsync(urlNueva);
            throw;
        }
    }

    // Limpieza de un blob que ya no se usa. Si Azure no responde, el blob queda huérfano (no afecta al usuario
    // y la implementación lo deja en el log); no tiene caso fallar una operación que ya quedó guardada.
    // Sin CancellationToken: si el cliente se desconectó, la limpieza igual debe terminar.
    private async Task EliminarSinFallarAsync(string url)
    {
        try
        {
            await _blobs.EliminarImagenAsync(url, CancellationToken.None);
        }
        catch (ServiceUnavailableException)
        {
            // Ya registrado por la implementación
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
