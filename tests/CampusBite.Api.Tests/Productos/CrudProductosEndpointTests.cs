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

// JJ-Sprint4 27/10/2026: TEC-09. GET/PUT/PATCH/DELETE /api/cafeterias/{id}/productos con la API en memoria.
// Verifica acceso, validación y que cada acción llega al repositorio correctamente.
public class CrudProductosEndpointTests : IClassFixture<ApiDePrueba>
{
    private static readonly Guid IdCafeteria = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    private static readonly Guid IdProducto = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly string RutaProductos = $"/api/cafeterias/{IdCafeteria}/productos";
    private static readonly string RutaProducto = $"/api/cafeterias/{IdCafeteria}/productos/{IdProducto}";

    private readonly ApiDePrueba _api;
    private readonly RepositorioFalso _repositorio = new();

    public CrudProductosEndpointTests(ApiDePrueba api) => _api = api;

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

    private static Dictionary<string, object?> CuerpoValido() => new()
    {
        ["nombre"] = "Chilaquiles verdes",
        ["descripcion"] = "Con pollo",
        ["precio"] = 55.50m,
        ["disponible"] = true
    };

    // --- GET Listar ---

    [Fact]
    public async Task Listar_Sin_sesion_devuelve_401()
        => Assert.Equal(HttpStatusCode.Unauthorized,
            (await _api.CreateClient().GetAsync(RutaProductos)).StatusCode);

    [Fact]
    public async Task Listar_Alumno_devuelve_403()
        => Assert.Equal(HttpStatusCode.Forbidden,
            (await Cliente(RolUsuario.Alumno).GetAsync(RutaProductos)).StatusCode);

    [Fact]
    public async Task Listar_AdminCafeteria_devuelve_200_con_lista()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).GetAsync(RutaProductos);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!;
        Assert.NotNull(pagina["items"]!.AsArray());
        Assert.Equal((1, 20), ((int)pagina["pagina"]!, (int)pagina["tamanoPagina"]!));
    }

    // JY-Sprint4 05/11/2026: paginado
    [Fact]
    public async Task Listar_pagina_busca_y_filtra_por_categoria()
    {
        var bebidas = Guid.NewGuid();
        for (var n = 1; n <= 45; n++)
        {
            _repositorio.Productos.Add(new Application.Productos.Dtos.ProductoDto
            {
                IdProducto = Guid.NewGuid(), IdCafeteria = IdCafeteria, Nombre = $"Producto {n:00}",
                IdCategoria = n % 3 == 0 ? bebidas : null
            });
        }

        var pagina = JsonNode.Parse(await (await Cliente(RolUsuario.AdminCafeteria)
            .GetAsync($"{RutaProductos}?pagina=2&tamanoPagina=10&buscar=producto&idCategoria={bebidas}")).Content.ReadAsStringAsync())!;

        Assert.Equal(15, (int)pagina["total"]!);                          // 45 / 3 son bebidas
        Assert.Equal(5, pagina["items"]!.AsArray().Count);                 // segunda página de 10: quedan 5
        Assert.Equal(("producto", (Guid?)bebidas, 10, 10), _repositorio.UltimoListado);
    }

    [Theory]
    [InlineData("tamanoPagina=101", "TamanoPagina")]
    [InlineData("pagina=0", "Pagina")]
    [InlineData("idCategoria=no-es-guid", "idCategoria")]
    public async Task Listar_con_parametros_invalidos_es_400(string consulta, string campo)
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).GetAsync($"{RutaProductos}?{consulta}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!["errors"]!.AsObject();
        Assert.Contains(errores, e => string.Equals(e.Key, campo, StringComparison.OrdinalIgnoreCase));
    }

    // --- PUT Actualizar ---

    [Fact]
    public async Task Actualizar_Sin_sesion_devuelve_401()
        => Assert.Equal(HttpStatusCode.Unauthorized,
            (await _api.CreateClient().PutAsJsonAsync(RutaProducto, CuerpoValido())).StatusCode);

    [Fact]
    public async Task Actualizar_Precio_cero_devuelve_400()
    {
        var cuerpo = CuerpoValido();
        cuerpo["precio"] = 0m;
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PutAsJsonAsync(RutaProducto, cuerpo);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Null(_repositorio.UltimosDatos);
    }

    [Fact]
    public async Task Actualizar_Nombre_duplicado_devuelve_409()
    {
        _repositorio.Error = new ConflictException("Ya existe un producto con el nombre \"Chilaquiles verdes\" en esta cafetería");
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PutAsJsonAsync(RutaProducto, CuerpoValido());
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Actualizar_Producto_inexistente_devuelve_404()
    {
        _repositorio.Error = new NotFoundException("El producto no existe");
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PutAsJsonAsync(RutaProducto, CuerpoValido());
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Actualizar_AdminCafeteria_valido_devuelve_204()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PutAsJsonAsync(RutaProducto, CuerpoValido());
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal("Chilaquiles verdes", _repositorio.UltimosDatos!.Nombre);
    }

    // --- PATCH Toggle disponible ---

    [Fact]
    public async Task ToggleDisponible_AdminCafeteria_devuelve_204()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria)
            .PatchAsJsonAsync($"{RutaProducto}/disponible", false);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(false, _repositorio.UltimoToggle);
    }

    // --- DELETE Eliminar ---

    [Fact]
    public async Task Eliminar_Sin_sesion_devuelve_401()
        => Assert.Equal(HttpStatusCode.Unauthorized,
            (await _api.CreateClient().DeleteAsync(RutaProducto)).StatusCode);

    [Fact]
    public async Task Eliminar_Con_ordenes_activas_devuelve_409()
    {
        _repositorio.Error = new ConflictException("El producto tiene 2 orden(es) activa(s); desactívalo en su lugar");
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).DeleteAsync(RutaProducto);
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var detalle = (string?)JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!["detail"];
        Assert.Contains("2 orden(es)", detalle);
    }

    [Fact]
    public async Task Eliminar_AdminCafeteria_devuelve_204()
    {
        var respuesta = await Cliente(RolUsuario.AdminCafeteria).DeleteAsync(RutaProducto);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.True(_repositorio.Eliminado);
    }
}
