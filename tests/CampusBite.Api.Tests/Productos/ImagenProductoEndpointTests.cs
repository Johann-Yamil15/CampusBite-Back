using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CampusBite.Api.Tests.Seguridad;
using CampusBite.Application.Common.Security;
using CampusBite.Application.Productos;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusBite.Api.Tests.Productos;

// JY-Sprint4 27/10/2026: TEC-10. POST /api/cafeterias/{id}/productos/{idProducto}/imagen con la API real en memoria:
// servicio y validaciones reales; solo el almacén (Azure) y el repositorio (BD) son falsos.
public class ImagenProductoEndpointTests : IClassFixture<ApiDePrueba>
{
    private static readonly Guid IdCafeteria = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid IdProducto = Guid.Parse("00000000-0000-0000-0000-0000000000e1");
    private static readonly string Ruta = $"/api/cafeterias/{IdCafeteria}/productos/{IdProducto}/imagen";
    private static readonly byte[] PngMinimo = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private readonly ApiDePrueba _api;
    private readonly AlmacenFalso _almacen = new();
    private readonly RepositorioFalso _repositorio = new();

    public ImagenProductoEndpointTests(ApiDePrueba api)
    {
        _api = api;
    }

    private HttpClient Cliente(RolUsuario rol)
    {
        var fabrica = _api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IBlobService>();
            s.AddSingleton<IBlobService>(_almacen);
            s.RemoveAll<IProductoRepository>();
            s.AddSingleton<IProductoRepository>(_repositorio);
        }));
        var (token, _) = fabrica.Services.CreateScope().ServiceProvider
            .GetRequiredService<ITokenService>()
            .GenerarToken(Guid.NewGuid(), "prueba@ejemplo.test", rol);
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static MultipartFormDataContent Formulario(byte[] bytes, string tipo, string campo = "archivo")
    {
        var archivo = new ByteArrayContent(bytes);
        archivo.Headers.ContentType = MediaTypeHeaderValue.Parse(tipo);
        return new MultipartFormDataContent { { archivo, campo, "foto.png" } };
    }

    [Theory]
    [InlineData(RolUsuario.Alumno, HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminCafeteria, HttpStatusCode.OK)]
    [InlineData(RolUsuario.AdminSistema, HttpStatusCode.OK)]
    public async Task Solo_el_personal_de_cafeteria_sube_imagenes(RolUsuario rol, HttpStatusCode esperado)
    {
        var respuesta = await Cliente(rol).PostAsync(Ruta, Formulario(PngMinimo, "image/png"));

        Assert.Equal(esperado, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_es_401()
    {
        var respuesta = await _api.CreateClient().PostAsync(Ruta, Formulario(PngMinimo, "image/png"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Devuelve_la_url_guardada_y_borra_la_anterior()
    {
        _repositorio.ImagenActual = "https://campusbite.blob.core.windows.net/productos/vieja.png";

        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(PngMinimo, "image/png"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonObject>();
        var url = cuerpo!["imagenUrl"]!.GetValue<string>();
        Assert.StartsWith("https://", url);
        Assert.Equal(url, _repositorio.ImagenActual);
        Assert.Equal(["https://campusbite.blob.core.windows.net/productos/vieja.png"], _almacen.Eliminadas);
    }

    [Fact]
    public async Task Sin_archivo_es_400_con_el_campo_indicado()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria)
            .PostAsync(Ruta, Formulario(PngMinimo, "image/png", campo: "otroCampo"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(cuerpo!["errors"]!["Archivo"]);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("text/html")]
    public async Task Tipo_invalido_es_400(string tipo)
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(PngMinimo, tipo));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Empty(_almacen.Subidas);
    }

    [Fact]
    public async Task Contenido_falso_es_400()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria)
            .PostAsync(Ruta, Formulario("no soy una imagen"u8.ToArray(), "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Empty(_almacen.Subidas);
    }

    [Fact]
    public async Task Mas_de_5_MB_por_poco_es_400()
    {
        var bytes = new byte[ReglasImagenProducto.TamanoMaximoBytes + 1];
        PngMinimo.CopyTo(bytes, 0);

        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(bytes, "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Empty(_almacen.Subidas);
    }

    [Fact]
    public async Task Mucho_mas_de_5_MB_es_413_sin_leer_el_archivo()
    {
        var bytes = new byte[ReglasImagenProducto.TamanoMaximoBytes * 2];
        PngMinimo.CopyTo(bytes, 0);

        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(bytes, "image/png"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Contains("5 MB", cuerpo!["detail"]!.GetValue<string>());
        Assert.Empty(_almacen.Subidas);
    }

    [Fact]
    public async Task Si_Azure_no_responde_es_503_con_Retry_After()
    {
        _almacen.ErrorAlSubir = new ServiceUnavailableException("El servicio de imágenes no está disponible en este momento.");

        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(PngMinimo, "image/png"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), respuesta.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Si_la_BD_niega_es_403_y_no_queda_el_archivo_en_Azure()
    {
        _repositorio.Error = new ForbiddenException("Sin permiso para cambiar la imagen de este producto");

        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(PngMinimo, "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal([Assert.Single(_almacen.Subidas)], _almacen.Eliminadas);
    }
}
