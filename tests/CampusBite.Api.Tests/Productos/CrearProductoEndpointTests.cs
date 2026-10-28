using System.Globalization;
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

// JY-Sprint4 27/10/2026: TEC-09. POST /api/cafeterias/{id}/productos (multipart) y GET .../productos/{idProducto} con la API real en memoria:
// servicio y validaciones reales; solo el almacén (Azure) y el repositorio (BD) son falsos.
// La imagen opcional debe seguir exactamente las mismas reglas que POST .../imagen.
public class CrearProductoEndpointTests : IClassFixture<ApiDePrueba>
{
    private static readonly string Ruta = "/api/cafeterias/00000000-0000-0000-0000-0000000000c1/productos";
    private static readonly byte[] PngMinimo = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private readonly ApiDePrueba _api;
    private readonly AlmacenFalso _almacen = new();
    private readonly RepositorioFalso _repositorio = new();

    public CrearProductoEndpointTests(ApiDePrueba api)
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

    private static MultipartFormDataContent Formulario(
        string nombre = "Chilaquiles verdes", string? precio = "55.50", byte[]? imagen = null, string tipo = "image/png")
    {
        var formulario = new MultipartFormDataContent
        {
            { new StringContent(nombre), "nombre" },
            { new StringContent("Con pollo y crema"), "descripcion" }
        };
        if (precio is not null)
        {
            formulario.Add(new StringContent(precio), "precio");
        }
        if (imagen is not null)
        {
            var archivo = new ByteArrayContent(imagen);
            archivo.Headers.ContentType = MediaTypeHeaderValue.Parse(tipo);
            formulario.Add(archivo, "archivo", "foto.png");
        }
        return formulario;
    }

    [Theory]
    [InlineData(RolUsuario.Alumno, HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminCafeteria, HttpStatusCode.Created)]
    [InlineData(RolUsuario.AdminSistema, HttpStatusCode.Created)]
    public async Task Solo_el_personal_de_cafeteria_crea_productos(RolUsuario rol, HttpStatusCode esperado)
    {
        var respuesta = await Cliente(rol).PostAsync(Ruta, Formulario());

        Assert.Equal(esperado, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_imagen_crea_el_producto_sin_foto()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario());

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("Chilaquiles verdes", cuerpo!["nombre"]!.GetValue<string>());
        Assert.Equal(55.50m, cuerpo["precio"]!.GetValue<decimal>());
        Assert.Null(cuerpo["imagenUrl"]);
        Assert.True(cuerpo["disponible"]!.GetValue<bool>());
        Assert.Empty(_almacen.Subidas);
    }

    [Fact]
    public async Task El_201_trae_Location_al_GET_del_producto_y_ese_GET_lo_devuelve()
    {
        var cliente = Cliente(RolUsuario.AdminCafeteria);

        var respuesta = await cliente.PostAsync(Ruta, Formulario());

        var id = (await respuesta.Content.ReadFromJsonAsync<JsonObject>())!["idProducto"]!.GetValue<Guid>();
        Assert.Equal($"{Ruta}/{id}", respuesta.Headers.Location?.AbsolutePath);
        var leido = await cliente.GetFromJsonAsync<JsonObject>(respuesta.Headers.Location);
        Assert.Equal(id, leido!["idProducto"]!.GetValue<Guid>());
        Assert.Equal("Chilaquiles verdes", leido["nombre"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(RolUsuario.Alumno)]
    [InlineData(RolUsuario.AdminCafeteria)]
    public async Task Cualquier_usuario_con_sesion_puede_pedir_un_producto_y_si_no_existe_es_404(RolUsuario rol)
    {
        var respuesta = await Cliente(rol).GetAsync($"{Ruta}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("El producto no existe.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sin_sesion_el_GET_de_un_producto_es_401()
    {
        var cliente = Cliente(RolUsuario.Alumno);
        cliente.DefaultRequestHeaders.Authorization = null;

        var respuesta = await cliente.GetAsync($"{Ruta}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Con_imagen_la_sube_y_devuelve_su_url()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(imagen: PngMinimo));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(Assert.Single(_almacen.Subidas), cuerpo!["imagenUrl"]!.GetValue<string>());
    }

    [Fact]
    public async Task El_precio_se_lee_igual_sin_importar_el_idioma_del_servidor()
    {
        var cultura = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-ES");   // ahí el separador decimal es ","

            var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(precio: "55.50"));

            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
            Assert.Equal(55.50m, _repositorio.UltimosDatos!.Precio);
        }
        finally
        {
            CultureInfo.CurrentCulture = cultura;
        }
    }

    [Fact]
    public async Task Datos_invalidos_son_400_por_campo_y_no_suben_la_imagen()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria)
            .PostAsync(Ruta, Formulario(nombre: "X", precio: null, imagen: PngMinimo));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = (await respuesta.Content.ReadFromJsonAsync<JsonObject>())!["errors"]!.AsObject();
        Assert.True(errores.ContainsKey("Precio"));
        Assert.Empty(_almacen.Subidas);
        Assert.Null(_repositorio.UltimosDatos);
    }

    [Fact]
    public async Task Un_formulario_danado_es_400_y_no_500()
    {
        var cuerpo = new StringContent("--x\r\nContent-Disposition: form-data; name=\"nombre\"\r\n\r\nSin cierre");
        cuerpo.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=x");

        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("No se pudo leer el formulario", await respuesta.Content.ReadAsStringAsync());
        Assert.Null(_repositorio.UltimosDatos);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10.555")]
    [InlineData("abc")]
    public async Task Precio_invalido_es_400(string precio)
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(precio: precio));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Null(_repositorio.UltimosDatos);
    }

    // Las mismas reglas de imagen que POST .../imagen: tipo, contenido real y tamaño
    [Theory]
    [InlineData("image/gif")]
    [InlineData("text/html")]
    public async Task Imagen_de_tipo_invalido_es_400_y_no_crea_el_producto(string tipo)
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(imagen: PngMinimo, tipo: tipo));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Null(_repositorio.UltimosDatos);
    }

    [Fact]
    public async Task Imagen_con_contenido_falso_es_400_y_no_crea_el_producto()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria)
            .PostAsync(Ruta, Formulario(imagen: "no soy una imagen"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Null(_repositorio.UltimosDatos);
    }

    [Fact]
    public async Task Imagen_de_poco_mas_de_5_MB_es_400_y_no_crea_el_producto()
    {
        var bytes = new byte[ReglasImagenProducto.TamanoMaximoBytes + 1];
        PngMinimo.CopyTo(bytes, 0);

        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(imagen: bytes));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Null(_repositorio.UltimosDatos);
    }

    [Fact]
    public async Task Imagen_de_mucho_mas_de_5_MB_es_413()
    {
        var bytes = new byte[ReglasImagenProducto.TamanoMaximoBytes * 2];
        PngMinimo.CopyTo(bytes, 0);

        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(imagen: bytes));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
        Assert.Null(_repositorio.UltimosDatos);
    }

    [Fact]
    public async Task Nombre_repetido_es_409_y_no_queda_la_imagen_en_Azure()
    {
        _repositorio.Error = new ConflictException("Ya existe un producto con el nombre \"Chilaquiles verdes\" en esta cafetería");

        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(imagen: PngMinimo));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal([Assert.Single(_almacen.Subidas)], _almacen.Eliminadas);
    }

    [Fact]
    public async Task Si_Azure_no_responde_es_503_con_Retry_After_y_no_crea_el_producto()
    {
        _almacen.ErrorAlSubir = new ServiceUnavailableException("El servicio de imágenes no está disponible en este momento.");

        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PostAsync(Ruta, Formulario(imagen: PngMinimo));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), respuesta.Headers.RetryAfter?.Delta);
        Assert.Null(_repositorio.UltimosDatos);
    }
}
