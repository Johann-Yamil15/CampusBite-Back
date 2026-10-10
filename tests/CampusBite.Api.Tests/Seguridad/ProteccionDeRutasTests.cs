using System.Net;
using System.Net.Http.Json;
using CampusBite.Domain.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CampusBite.Api.Tests.Seguridad;

// JY-Sprint2 10/10/2026: TEC-07. Verifica la protección de rutas por rol con la API real en memoria
public class ProteccionDeRutasTests : IClassFixture<ApiDePrueba>
{
    private readonly ApiDePrueba _api;

    public ProteccionDeRutasTests(ApiDePrueba api)
    {
        _api = api;
    }

    // Matriz de permisos: quién entra (200) y quién no (403) a cada política
    [Theory]
    [InlineData(RolUsuario.Alumno,         "/pruebas/alumno",   HttpStatusCode.OK)]
    [InlineData(RolUsuario.AdminCafeteria, "/pruebas/alumno",   HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminSistema,   "/pruebas/alumno",   HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.Alumno,         "/pruebas/personal", HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminCafeteria, "/pruebas/personal", HttpStatusCode.OK)]
    [InlineData(RolUsuario.AdminSistema,   "/pruebas/personal", HttpStatusCode.OK)]
    [InlineData(RolUsuario.Alumno,         "/pruebas/admin",    HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminCafeteria, "/pruebas/admin",    HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminSistema,   "/pruebas/admin",    HttpStatusCode.OK)]
    public async Task Cada_rol_entra_solo_a_sus_rutas(RolUsuario rol, string ruta, HttpStatusCode esperado)
    {
        var respuesta = await _api.ClienteCon(rol).GetAsync(ruta);

        Assert.Equal(esperado, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_endpoint_sin_atributo_exige_sesion_por_defecto()
    {
        var respuesta = await _api.CreateClient().GetAsync("/pruebas/sin-atributo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_endpoint_AllowAnonymous_no_pide_token()
    {
        var respuesta = await _api.CreateClient().GetAsync("/pruebas/publica");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_token_responde_401_con_ProblemDetails()
    {
        var respuesta = await _api.CreateClient().GetAsync("/pruebas/alumno");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Unauthorized", problema?.Title);
        Assert.Equal("Necesitas iniciar sesión para usar este recurso.", problema?.Detail);
    }

    [Fact]
    public async Task Rol_sin_permiso_responde_403_con_ProblemDetails()
    {
        var respuesta = await _api.ClienteCon(RolUsuario.Alumno).GetAsync("/pruebas/admin");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Forbidden", problema?.Title);
    }

    [Fact]
    public async Task Token_vencido_responde_401_y_avisa_que_vencio()
    {
        var token = ApiDePrueba.TokenManual(nameof(RolUsuario.Alumno), DateTime.UtcNow.AddMinutes(-10));

        var respuesta = await _api.ClienteConToken(token).GetAsync("/pruebas/alumno");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Contains("invalid_token", respuesta.Headers.WwwAuthenticate.ToString());
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Tu sesión venció. Inicia sesión de nuevo.", problema?.Detail);
    }

    [Fact]
    public async Task Token_firmado_con_otra_llave_se_rechaza()
    {
        var token = ApiDePrueba.TokenManual(nameof(RolUsuario.AdminSistema), DateTime.UtcNow.AddHours(1),
            llave: "otra_llave_que_no_es_la_del_servidor_de_32_caracteres");

        var respuesta = await _api.ClienteConToken(token).GetAsync("/pruebas/admin");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("admin")]           // nombre del catálogo de la BD, no del enum
    [InlineData("adminsistema")]    // mayúsculas distintas
    [InlineData("3")]               // número del rol
    public async Task Un_rol_que_no_es_exactamente_el_del_enum_no_da_permiso(string rol)
    {
        var token = ApiDePrueba.TokenManual(rol, DateTime.UtcNow.AddHours(1));

        var respuesta = await _api.ClienteConToken(token).GetAsync("/pruebas/admin");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_usuario_actual_sale_del_token_y_no_de_la_peticion()
    {
        var idDelToken = Guid.NewGuid();
        var otroId = Guid.NewGuid();

        var respuesta = await _api.ClienteCon(RolUsuario.Alumno, idDelToken)
            .GetFromJsonAsync<QuienSoy>($"/pruebas/quien-soy?idUsuario={otroId}");

        Assert.Equal(idDelToken, respuesta?.Id);
        Assert.Equal(otroId, respuesta?.IdDeLaQuery);
    }

    [Fact]
    public async Task Me_devuelve_id_y_rol_de_la_sesion()
    {
        var id = Guid.NewGuid();

        var respuesta = await _api.ClienteCon(RolUsuario.AdminCafeteria, id).GetFromJsonAsync<Sesion>("/api/auth/me");

        Assert.Equal(id, respuesta?.Id);
        Assert.Equal(nameof(RolUsuario.AdminCafeteria), respuesta?.Rol);
        Assert.Equal("prueba@ejemplo.test", respuesta?.Correo);
    }

    private record QuienSoy(Guid Id, Guid? IdDeLaQuery);
    private record Sesion(Guid Id, string Correo, string Rol);
}
