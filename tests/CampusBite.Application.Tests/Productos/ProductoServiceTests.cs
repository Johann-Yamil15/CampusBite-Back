using CampusBite.Application.Productos;
using CampusBite.Application.Productos.Dtos;
using CampusBite.Application.Tests.Cafeterias;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace CampusBite.Application.Tests.Productos;

// JY-Sprint4 27/10/2026: TEC-09/TEC-10. Reglas de ProductoService con almacén, repositorio y sesión falsos
public class ProductoServiceTests
{
    private static readonly Guid IdCafeteria = Guid.NewGuid();
    private static readonly Guid IdProducto = Guid.NewGuid();
    private const string UrlAnterior = "https://campusbite.blob.core.windows.net/productos/vieja.png";

    private readonly FakeBlobService _blobs = new();
    private readonly FakeProductoRepository _repositorio = new();
    private readonly FakeUsuarioActual _usuario = new() { Rol = RolUsuario.AdminCafeteria };
    private readonly FakeLogger<ProductoService> _logger = new();
    private readonly ProductoService _servicio;

    public ProductoServiceTests()
    {
        _servicio = new ProductoService(_repositorio, _blobs, _usuario, _logger);
    }

    private static GuardarProductoDto DatosProducto() => new()
    {
        Nombre = "  Chilaquiles verdes ", Descripcion = "   ", Precio = 55.50m, Disponible = true
    };

    private Task<ProductoDto> Crear(ArchivoImagen? imagen) => _servicio.CrearAsync(IdCafeteria, DatosProducto(), imagen);

    // ---- Alta de producto (TEC-09) ----

    [Fact]
    public async Task Crear_sin_imagen_normaliza_los_datos_y_no_toca_Azure()
    {
        var creado = await Crear(imagen: null);

        var alta = _repositorio.UltimaCreacion!.Value;
        Assert.Equal((IdCafeteria, _usuario.Id, (string?)null), (alta.IdCafeteria, alta.IdActor, alta.Url));
        Assert.Equal(new DatosProducto("Chilaquiles verdes", null, 55.50m, null, true), alta.Datos);
        Assert.NotEqual(Guid.Empty, alta.IdProducto);
        Assert.Equal(alta.IdProducto, creado.IdProducto);
        Assert.Empty(_blobs.Subidas);
    }

    [Theory]
    [InlineData("  Con pollo y crema ", "Con pollo y crema")]
    [InlineData(null, null)]
    [InlineData("", null)]
    public async Task Crear_recorta_la_descripcion_y_la_vacia_se_guarda_como_null(string? enviada, string? guardada)
    {
        var dto = DatosProducto();
        dto.Descripcion = enviada;

        await _servicio.CrearAsync(IdCafeteria, dto, null);

        Assert.Equal(guardada, _repositorio.UltimaCreacion!.Value.Datos.Descripcion);
    }

    [Fact]
    public async Task Crear_con_imagen_la_sube_con_el_id_del_nuevo_producto_y_guarda_su_url()
    {
        var creado = await Crear(Archivo(Png(), "image/png"));

        var subida = Assert.Single(_blobs.Subidas);
        var alta = _repositorio.UltimaCreacion!.Value;
        Assert.Equal(alta.IdProducto, subida.IdProducto);
        Assert.Equal(subida.Url, alta.Url);
        Assert.Equal(subida.Url, creado.ImagenUrl);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData(null)]
    public async Task Crear_con_imagen_de_tipo_invalido_no_crea_nada(string? tipo)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => Crear(Archivo(Png(), tipo)));

        Assert.Empty(_blobs.Subidas);
        Assert.Null(_repositorio.UltimaCreacion);
    }

    [Fact]
    public async Task Crear_con_imagen_de_mas_de_5_MB_no_crea_nada()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => Crear(Archivo(Png((int)ReglasImagenProducto.TamanoMaximoBytes + 1), "image/png")));

        Assert.Empty(_blobs.Subidas);
        Assert.Null(_repositorio.UltimaCreacion);
    }

    [Fact]
    public async Task Crear_con_contenido_falso_no_crea_nada()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => Crear(Archivo(Jpeg(), "image/webp")));

        Assert.Null(_repositorio.UltimaCreacion);
    }

    [Fact]
    public async Task Crear_si_la_BD_rechaza_borra_la_imagen_recien_subida()
    {
        _repositorio.Error = new ConflictException("Ya existe un producto con el nombre \"Chilaquiles verdes\" en esta cafetería");

        await Assert.ThrowsAsync<ConflictException>(() => Crear(Archivo(Png(), "image/png")));

        var subida = Assert.Single(_blobs.Subidas);
        Assert.Equal([subida.Url], _blobs.Eliminadas);
    }

    [Fact]
    public async Task Crear_si_Azure_no_responde_es_503_y_no_crea_el_producto()
    {
        _blobs.ErrorAlSubir = new ServiceUnavailableException("No disponible");

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => Crear(Archivo(Png(), "image/png")));
        Assert.Null(_repositorio.UltimaCreacion);
    }

    // ---- Cambio de imagen (TEC-10) ----

    // Archivos mínimos con la firma real de cada formato
    private static byte[] Png(int tamano = 64) => Rellenar([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], tamano);
    private static byte[] Jpeg(int tamano = 64) => Rellenar([0xFF, 0xD8, 0xFF, 0xE0], tamano);
    private static byte[] Webp(int tamano = 64) => Rellenar("RIFF\0\0\0\0WEBPVP8 "u8.ToArray(), tamano);

    private static byte[] Rellenar(byte[] firma, int tamano)
    {
        var bytes = new byte[tamano];
        firma.CopyTo(bytes, 0);
        return bytes;
    }

    private static ArchivoImagen Archivo(byte[] bytes, string? tipo, long? longitud = null) =>
        new(new MemoryStream(bytes), tipo, longitud ?? bytes.Length);

    private Task<ImagenProductoDto> Subir(ArchivoImagen archivo) =>
        _servicio.SubirImagenAsync(IdCafeteria, IdProducto, archivo);

    [Theory]
    [InlineData("image/png", "image/png")]
    [InlineData("image/jpeg", "image/jpeg")]
    [InlineData("image/jpg", "image/jpeg")]            // alias que mandan algunos celulares
    [InlineData("IMAGE/WEBP", "image/webp")]
    [InlineData("image/png; charset=binary", "image/png")]
    public async Task Sube_GuardaLaUrl_ConElActorDeLaSesion_YDevuelveLaUrl(string tipoEnviado, string tipoGuardado)
    {
        var bytes = tipoGuardado switch { "image/png" => Png(), "image/jpeg" => Jpeg(), _ => Webp() };

        var resultado = await Subir(Archivo(bytes, tipoEnviado));

        var subida = Assert.Single(_blobs.Subidas);
        Assert.Equal((IdProducto, tipoGuardado), (subida.IdProducto, subida.ContentType));
        Assert.Equal(bytes, subida.Bytes);
        Assert.Equal((IdCafeteria, IdProducto, _usuario.Id, subida.Url), _repositorio.UltimaActualizacion);
        Assert.Equal(subida.Url, resultado.ImagenUrl);
    }

    [Fact]
    public async Task Si_ya_tenia_imagen_borra_la_anterior_despues_de_guardar_la_nueva()
    {
        _repositorio.ImagenActual = UrlAnterior;

        await Subir(Archivo(Png(), "image/png"));

        Assert.Equal([UrlAnterior], _blobs.Eliminadas);
    }

    [Fact]
    public async Task Sin_imagen_anterior_no_borra_nada()
    {
        await Subir(Archivo(Png(), "image/png"));

        Assert.Empty(_blobs.Eliminadas);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]                      // un SVG puede llevar scripts
    [InlineData("application/octet-stream")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Tipo_no_permitido_se_rechaza_sin_subir(string? tipo)
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Subir(Archivo(Png(), tipo)));

        Assert.Contains("JPEG, PNG o WebP", error.Message);
        Assert.Empty(_blobs.Subidas);
    }

    [Fact]
    public async Task Mas_de_5_MB_se_rechaza_sin_leer_ni_subir()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Subir(Archivo(Png(), "image/png", longitud: ReglasImagenProducto.TamanoMaximoBytes + 1)));

        Assert.Contains("5 MB", error.Message);
        Assert.Empty(_blobs.Subidas);
    }

    [Fact]
    public async Task Si_el_cliente_miente_con_la_longitud_cuentan_los_bytes_reales()
    {
        var grande = Png((int)ReglasImagenProducto.TamanoMaximoBytes + 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Subir(Archivo(grande, "image/png", longitud: 1000)));
        Assert.Empty(_blobs.Subidas);
    }

    [Fact]
    public async Task Exactamente_5_MB_se_acepta()
    {
        await Subir(Archivo(Png((int)ReglasImagenProducto.TamanoMaximoBytes), "image/png"));

        Assert.Single(_blobs.Subidas);
    }

    [Fact]
    public async Task Archivo_vacio_se_rechaza()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Subir(Archivo([], "image/png")));

        Assert.Contains("vacío", error.Message);
    }

    [Theory]
    [InlineData("image/png")]                          // dice PNG pero es JPEG
    [InlineData("image/webp")]
    public async Task Contenido_que_no_coincide_con_el_tipo_se_rechaza(string tipo)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => Subir(Archivo(Jpeg(), tipo)));
        Assert.Empty(_blobs.Subidas);
    }

    [Fact]
    public async Task Un_html_renombrado_como_imagen_se_rechaza()
    {
        var html = "<html><script>alert(1)</script></html>"u8.ToArray();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Subir(Archivo(html, "image/jpeg")));
        Assert.Empty(_blobs.Subidas);
    }

    [Fact]
    public async Task Si_la_BD_rechaza_se_borra_la_imagen_recien_subida_y_se_propaga_el_error()
    {
        _repositorio.Error = new ForbiddenException("Sin permiso para cambiar la imagen de este producto");

        await Assert.ThrowsAsync<ForbiddenException>(() => Subir(Archivo(Png(), "image/png")));

        var subida = Assert.Single(_blobs.Subidas);
        Assert.Equal([subida.Url], _blobs.Eliminadas);
    }

    [Fact]
    public async Task Si_Azure_no_responde_al_subir_es_503_y_no_toca_la_BD()
    {
        _blobs.ErrorAlSubir = new ServiceUnavailableException("No disponible");

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => Subir(Archivo(Png(), "image/png")));
        Assert.Null(_repositorio.UltimaActualizacion);
    }

    [Fact]
    public async Task Si_no_se_puede_borrar_la_anterior_la_subida_igual_es_exitosa()
    {
        _repositorio.ImagenActual = UrlAnterior;
        _blobs.ErrorAlEliminar = new ServiceUnavailableException("No disponible");

        var resultado = await Subir(Archivo(Png(), "image/png"));

        Assert.Equal(_blobs.Subidas[0].Url, resultado.ImagenUrl);
        var aviso = Assert.Single(_logger.Registros);
        Assert.Equal(LogLevel.Warning, aviso.Nivel);
        Assert.Equal($"Imagen huérfana en Azure Blob Storage: no se pudo borrar {UrlAnterior} (fue reemplazada por una nueva)",
            aviso.Mensaje);
        Assert.Same(_blobs.ErrorAlEliminar, aviso.Excepcion);
    }

    [Fact]
    public async Task Si_la_BD_rechaza_y_tampoco_se_puede_borrar_la_nueva_queda_el_aviso_y_se_propaga_el_error_de_la_BD()
    {
        _repositorio.Error = new ForbiddenException("Sin permiso para cambiar la imagen de este producto");
        _blobs.ErrorAlEliminar = new ServiceUnavailableException("No disponible");

        await Assert.ThrowsAsync<ForbiddenException>(() => Subir(Archivo(Png(), "image/png")));

        var aviso = Assert.Single(_logger.Registros);
        Assert.Equal(LogLevel.Warning, aviso.Nivel);
        Assert.Equal($"Imagen huérfana en Azure Blob Storage: no se pudo borrar {_blobs.Subidas[0].Url} (la BD rechazó el guardado)",
            aviso.Mensaje);
    }

    [Fact]
    public async Task Si_la_limpieza_sale_bien_no_deja_avisos()
    {
        _repositorio.ImagenActual = UrlAnterior;

        await Subir(Archivo(Png(), "image/png"));

        Assert.Empty(_logger.Registros);
    }

    [Fact]
    public async Task Reintentar_la_misma_subida_deja_una_sola_imagen_y_borra_la_del_intento_anterior()
    {
        var primera = await Subir(Archivo(Png(), "image/png"));
        var segunda = await Subir(Archivo(Png(), "image/png"));

        Assert.NotEqual(primera.ImagenUrl, segunda.ImagenUrl);
        Assert.Equal([primera.ImagenUrl], _blobs.Eliminadas);
        Assert.Equal(segunda.ImagenUrl, _repositorio.ImagenActual);
    }
}

// Guarda lo que se registra para revisar nivel, mensaje ya armado y excepción
public class FakeLogger<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Mensaje, Exception? Excepcion)> Registros { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Registros.Add((logLevel, formatter(state, exception), exception));
}

public class FakeBlobService : IBlobService
{
    public List<(Guid IdProducto, string ContentType, byte[] Bytes, string Url)> Subidas { get; } = [];
    public List<string> Eliminadas { get; } = [];
    public Exception? ErrorAlSubir { get; set; }
    public Exception? ErrorAlEliminar { get; set; }

    public async Task<string> SubirImagenProductoAsync(Guid idProducto, Stream contenido, string contentType, CancellationToken ct = default)
    {
        if (ErrorAlSubir is not null)
        {
            throw ErrorAlSubir;
        }

        using var copia = new MemoryStream();
        await contenido.CopyToAsync(copia, ct);
        var url = $"https://campusbite.blob.core.windows.net/productos/{idProducto}_{Subidas.Count}.img";
        Subidas.Add((idProducto, contentType, copia.ToArray(), url));
        return url;
    }

    public Task EliminarImagenAsync(string urlBlob, CancellationToken ct = default)
    {
        if (ErrorAlEliminar is not null)
        {
            throw ErrorAlEliminar;
        }

        Eliminadas.Add(urlBlob);
        return Task.CompletedTask;
    }
}

public class FakeProductoRepository : IProductoRepository
{
    public string? ImagenActual { get; set; }
    public Exception? Error { get; set; }
    public (Guid IdCafeteria, Guid IdProducto, Guid IdActor, string Url)? UltimaActualizacion { get; private set; }
    public (Guid IdProducto, Guid IdCafeteria, Guid IdActor, DatosProducto Datos, string? Url)? UltimaCreacion { get; private set; }

    public Task<ProductoDto> CrearAsync(
        Guid idProducto, Guid idCafeteria, Guid idActor, DatosProducto datos, string? imagenUrl,
        CancellationToken cancellationToken = default)
    {
        if (Error is not null)
        {
            throw Error;
        }

        UltimaCreacion = (idProducto, idCafeteria, idActor, datos, imagenUrl);
        return Task.FromResult(new ProductoDto
        {
            IdProducto = idProducto, IdCafeteria = idCafeteria, IdCategoria = datos.IdCategoria, Nombre = datos.Nombre,
            Descripcion = datos.Descripcion, Precio = datos.Precio, ImagenUrl = imagenUrl, Disponible = datos.Disponible
        });
    }

    public Task<string?> ActualizarImagenAsync(
        Guid idCafeteria, Guid idProducto, Guid idActor, string imagenUrl, CancellationToken cancellationToken = default)
    {
        if (Error is not null)
        {
            throw Error;
        }

        UltimaActualizacion = (idCafeteria, idProducto, idActor, imagenUrl);
        var anterior = ImagenActual;
        ImagenActual = imagenUrl;
        return Task.FromResult(anterior);
    }
}
