using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CampusBite.Api.Tests.Common;
using CampusBite.Api.Tests.Seguridad;
using CampusBite.Application.Productos;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CampusBite.Api.Tests.Blob;

// AzureBlobService (internal) con el SDK oficial de Azure apuntando a ServidorBlobDePrueba (HTTP local).
// Se obtiene de la DI real de la API con la sección "AzureBlob" de la prueba: no se toca código de producción ni Azure.
public class AzureBlobServiceTests : IClassFixture<ApiDePrueba>
{
    private readonly ApiDePrueba _api;

    public AzureBlobServiceTests(ApiDePrueba api)
    {
        _api = api;
    }

    private (IBlobService Blob, LogEnMemoria Log) Crear(string? cadena, Dictionary<string, string?>? cambios = null)
    {
        var log = new LogEnMemoria();
        var valores = new Dictionary<string, string?>
        {
            ["AzureBlob:ConnectionString"] = cadena,
            ["AzureBlob:ContainerProductos"] = "productos-prueba",
            ["AzureBlob:MaxReintentos"] = "0",
            ["AzureBlob:EsperaInicialSegundos"] = "0.1",
            ["AzureBlob:EsperaMaximaSegundos"] = "0.2",
            ["AzureBlob:TiempoPorIntentoSegundos"] = "1"
        };
        foreach (var (k, v) in cambios ?? [])
        {
            valores[k] = v;
        }
        var fabrica = _api.WithWebHostBuilder(b =>
        {
            foreach (var (k, v) in valores)
            {
                b.UseSetting(k, v);
            }
            b.ConfigureLogging(l => l.AddProvider(log));
        });
        return (fabrica.Services.GetRequiredService<IBlobService>(), log);
    }

    private static string Prefijo(ServidorBlobDePrueba s) => $"{s.Base}/{ServidorBlobDePrueba.Cuenta}/productos-prueba/";

    [Theory]
    [InlineData("image/webp", "webp")]
    [InlineData("image/png", "png")]
    [InlineData("image/jpeg", "jpg")]
    public async Task Subir_manda_el_archivo_con_su_tipo_y_cache_y_devuelve_la_URL_permanente(string tipo, string extension)
    {
        await using var servidor = new ServidorBlobDePrueba();
        var (blob, _) = Crear(servidor.CadenaDeConexion);
        var id = Guid.NewGuid();
        var bytes = Enumerable.Range(0, 3000).Select(i => (byte)(i % 251)).ToArray();

        var url = await blob.SubirImagenProductoAsync(id, new MemoryStream(bytes), tipo);

        Assert.StartsWith(Prefijo(servidor) + id + "_", url);
        Assert.Matches($@"_\d{{8}}T\d{{9}}Z\.{extension}$", url);
        var put = Assert.Single(servidor.Peticiones, p => p.Metodo == "PUT");
        Assert.Equal(new Uri(url).AbsolutePath, put.Ruta);
        Assert.Equal(bytes, put.Cuerpo);
        Assert.Equal(tipo, put.Encabezados["x-ms-blob-content-type"]);
        Assert.Equal("public, max-age=31536000, immutable", put.Encabezados["x-ms-blob-cache-control"]);
        Assert.Equal("BlockBlob", put.Encabezados["x-ms-blob-type"]);
        Assert.StartsWith("SharedKey " + ServidorBlobDePrueba.Cuenta + ":", put.Encabezados["authorization"]);
    }

    [Fact]
    public async Task Cada_subida_crea_un_blob_nuevo_aunque_sea_el_mismo_producto()
    {
        await using var servidor = new ServidorBlobDePrueba();
        var (blob, _) = Crear(servidor.CadenaDeConexion);
        var id = Guid.NewGuid();

        var primera = await blob.SubirImagenProductoAsync(id, new MemoryStream([1, 2, 3]), "image/png");
        await Task.Delay(5);
        var segunda = await blob.SubirImagenProductoAsync(id, new MemoryStream([4, 5, 6]), "image/png");

        Assert.NotEqual(primera, segunda);
        Assert.Equal(2, servidor.Blobs.Count);
    }

    [Fact]
    public async Task Si_Azure_responde_error_la_subida_da_503_generico_y_registra_el_detalle()
    {
        await using var servidor = new ServidorBlobDePrueba(ServidorBlobDePrueba.Modo.Error500);
        var (blob, log) = Crear(servidor.CadenaDeConexion);

        var ex = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            blob.SubirImagenProductoAsync(Guid.NewGuid(), new MemoryStream([1]), "image/webp"));

        Assert.Contains("servicio de imágenes no está disponible", ex.Message);
        var error = Assert.Single(log.Registros, r => r.Nivel == LogLevel.Error && r.Categoria.EndsWith("AzureBlobService"));
        Assert.IsAssignableFrom<Azure.RequestFailedException>(error.Excepcion);
    }

    [Fact]
    public async Task Si_un_intento_pasa_su_tiempo_limite_la_subida_da_503()
    {
        await using var servidor = new ServidorBlobDePrueba(ServidorBlobDePrueba.Modo.Lento);
        var (blob, log) = Crear(servidor.CadenaDeConexion);
        var reloj = Stopwatch.StartNew();

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            blob.SubirImagenProductoAsync(Guid.NewGuid(), new MemoryStream([1]), "image/webp"));

        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(8), $"Tardó {reloj.Elapsed}");
        Assert.Contains(log.Registros, r => r.Nivel == LogLevel.Error && r.Excepcion is not null);
    }

    [Fact]
    public async Task Sin_servidor_la_subida_da_503()
    {
        var libre = new TcpListener(IPAddress.Loopback, 0);
        libre.Start();
        var puerto = ((IPEndPoint)libre.LocalEndpoint).Port;
        libre.Stop();
        var cadena = $"DefaultEndpointsProtocol=http;AccountName=cuentaprueba;AccountKey={Convert.ToBase64String(new byte[64])};" +
                     $"BlobEndpoint=http://127.0.0.1:{puerto}/cuentaprueba;";
        var (blob, _) = Crear(cadena, new() { ["AzureBlob:TiempoPorIntentoSegundos"] = "5" });

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            blob.SubirImagenProductoAsync(Guid.NewGuid(), new MemoryStream([1]), "image/webp"));
    }

    [Fact]
    public async Task Si_quien_llama_cancela_la_subida_sale_la_cancelacion_y_no_un_503()
    {
        await using var servidor = new ServidorBlobDePrueba(ServidorBlobDePrueba.Modo.Lento);
        var (blob, log) = Crear(servidor.CadenaDeConexion, new() { ["AzureBlob:TiempoPorIntentoSegundos"] = "30" });
        using var cancelar = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            blob.SubirImagenProductoAsync(Guid.NewGuid(), new MemoryStream([1]), "image/webp", cancelar.Token));

        Assert.DoesNotContain(log.Registros, r => r.Nivel == LogLevel.Error && r.Categoria.EndsWith("AzureBlobService"));
    }

    [Fact]
    public async Task Sin_cadena_de_conexion_da_503_y_lo_vuelve_a_revisar_en_cada_peticion()
    {
        var (blob, log) = Crear("");

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            blob.SubirImagenProductoAsync(Guid.NewGuid(), new MemoryStream([1]), "image/webp"));
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => blob.EliminarImagenAsync("http://x/y/z.webp"));

        // PublicationOnly: el fallo no queda guardado, cada petición vuelve a revisar y deja su motivo en el log
        Assert.Equal(2, log.Registros.Count(r => r.Nivel == LogLevel.Error && r.Mensaje.Contains("AzureBlob:ConnectionString")));
    }

    [Fact]
    public async Task Eliminar_borra_el_blob_de_este_contenedor()
    {
        await using var servidor = new ServidorBlobDePrueba();
        var (blob, _) = Crear(servidor.CadenaDeConexion);
        var url = await blob.SubirImagenProductoAsync(Guid.NewGuid(), new MemoryStream([1, 2]), "image/png");

        await blob.EliminarImagenAsync(url);

        Assert.Empty(servidor.Blobs);
        Assert.Contains(servidor.Peticiones, p => p.Metodo == "DELETE" && p.Ruta == new Uri(url).AbsolutePath);
    }

    [Fact]
    public async Task Eliminar_un_blob_que_ya_no_existe_no_es_error()
    {
        await using var servidor = new ServidorBlobDePrueba();
        var (blob, _) = Crear(servidor.CadenaDeConexion);

        await blob.EliminarImagenAsync(Prefijo(servidor) + "no-existe.webp");

        Assert.Single(servidor.Peticiones, p => p.Metodo == "DELETE");
    }

    [Fact]
    public async Task Eliminar_decodifica_el_nombre_del_blob_de_la_URL()
    {
        await using var servidor = new ServidorBlobDePrueba();
        var (blob, _) = Crear(servidor.CadenaDeConexion);

        await blob.EliminarImagenAsync(Prefijo(servidor) + "carpeta%2Fcon%20espacio.webp");

        var delete = Assert.Single(servidor.Peticiones, p => p.Metodo == "DELETE");
        Assert.EndsWith("/productos-prueba/carpeta/con%20espacio.webp", delete.Ruta.Replace(" ", "%20"));
    }

    [Theory]
    [InlineData("no es una url")]                                               // no absoluta
    [InlineData("https://otro-sitio.ejemplo.test/productos-prueba/foto.webp")]  // otro host
    [InlineData("{prefijo-otro}foto.webp")]                                     // otro contenedor
    [InlineData("{prefijo}")]                                                   // el contenedor mismo, sin blob
    public async Task Eliminar_ignora_URLs_que_no_son_de_un_blob_de_este_contenedor(string url)
    {
        await using var servidor = new ServidorBlobDePrueba();
        var (blob, _) = Crear(servidor.CadenaDeConexion);
        url = url.Replace("{prefijo}", Prefijo(servidor))
                 .Replace("{prefijo-otro}", $"{servidor.Base}/{ServidorBlobDePrueba.Cuenta}/otro-contenedor/");

        await blob.EliminarImagenAsync(url);

        Assert.Empty(servidor.Peticiones);
    }

    [Fact]
    public async Task Si_Azure_falla_al_eliminar_da_503_y_avisa_que_queda_huerfano()
    {
        await using var servidor = new ServidorBlobDePrueba(ServidorBlobDePrueba.Modo.Error500);
        var (blob, log) = Crear(servidor.CadenaDeConexion);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => blob.EliminarImagenAsync(Prefijo(servidor) + "foto.webp"));

        var aviso = Assert.Single(log.Registros, r => r.Nivel == LogLevel.Warning && r.Categoria.EndsWith("AzureBlobService"));
        Assert.Contains("huérfano", aviso.Mensaje);
    }

    [Fact]
    public async Task Si_quien_llama_cancela_el_borrado_sale_la_cancelacion()
    {
        await using var servidor = new ServidorBlobDePrueba(ServidorBlobDePrueba.Modo.Lento);
        var (blob, _) = Crear(servidor.CadenaDeConexion, new() { ["AzureBlob:TiempoPorIntentoSegundos"] = "30" });
        using var cancelar = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            blob.EliminarImagenAsync(Prefijo(servidor) + "foto.webp", cancelar.Token));
    }
}
