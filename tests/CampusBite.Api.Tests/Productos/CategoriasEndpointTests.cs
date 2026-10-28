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

// JJ-Sprint4 27/10/2026: TEC-09. GET/POST/DELETE /api/cafeterias/{id}/categorias con la API en memoria.
public class CategoriasEndpointTests : IClassFixture<ApiDePrueba>
{
    private static readonly Guid IdCafeteria = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid IdCategoria = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly string RutaCategorias = $"/api/cafeterias/{IdCafeteria}/categorias";
    private static readonly string RutaCategoria = $"/api/cafeterias/{IdCafeteria}/categorias/{IdCategoria}";

    private readonly ApiDePrueba _api;
    private readonly RepositorioFalso _repositorio = new();

    public CategoriasEndpointTests(ApiDePrueba api) => _api = api;

    private HttpClient Cliente(RolUsuario rol)
    {
        var fabrica = _api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IProductoRepository>();
            s.AddSingleton<IProductoRepository>(_repositorio);
        }));
        var (token, _) = fabrica.Services.CreateScope().ServiceProvider
            .GetRequiredService<ITokenService>()
            .GenerarToken(Guid.NewGuid(), "prueba@ejemplo.test", rol);
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    // --- GET Listar ---

    [Fact]
    public async Task Listar_Sin_sesion_devuelve_401()
        => Assert.Equal(HttpStatusCode.Unauthorized,
            (await _api.CreateClient().GetAsync(RutaCategorias)).StatusCode);

    [Fact]
    public async Task Listar_Alumno_puede_ver_categorias_devuelve_200()
    {
        var respuesta = await Cliente(RolUsuario.Alumno).GetAsync(RutaCategorias);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    // --- POST Crear ---

    [Fact]
    public async Task Crear_Alumno_devuelve_403()
        => Assert.Equal(HttpStatusCode.Forbidden,
            (await Cliente(RolUsuario.Alumno).PostAsJsonAsync(RutaCategorias, new { nombre = "Bebidas" })).StatusCode);

    [Fact]
    public async Task Crear_Nombre_en_blanco_devuelve_400()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria)
            .PostAsJsonAsync(RutaCategorias, new { nombre = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_Nombre_duplicado_devuelve_409()
    {
        _repositorio.Error = new ConflictException("Ya existe una categoría con ese nombre en esta cafetería");
        var respuesta = await Cliente(RolUsuario.AdminCafeteria)
            .PostAsJsonAsync(RutaCategorias, new { nombre = "Bebidas" });
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_AdminCafeteria_valido_devuelve_201_con_id()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria)
            .PostAsJsonAsync(RutaCategorias, new { nombre = "Bebidas" });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var json = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!;
        Assert.False(string.IsNullOrEmpty((string?)json["nombre"]));
        Assert.NotEqual(Guid.Empty, (Guid)(json["idCategoria"]?.GetValue<Guid>() ?? Guid.Empty));
    }

    // --- DELETE Eliminar ---

    [Fact]
    public async Task Eliminar_Categoria_inexistente_devuelve_404()
    {
        _repositorio.Error = new NotFoundException("La categoría no existe");
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).DeleteAsync(RutaCategoria);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Eliminar_AdminCafeteria_devuelve_204()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).DeleteAsync(RutaCategoria);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }
}
