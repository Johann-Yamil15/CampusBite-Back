using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace CampusBite.Api.Tests.Seguridad;

// JY-Sprint3 20/10/2026: la API se niega a arrancar con una configuración de JWT insegura o incompleta.
// Las pruebas de mutación mostraron que nada probaba estas reglas (se podía relajar "mínimo 32 bytes" sin que
// fallara algo). Es mejor que no arranque a que firme tokens con una llave débil.
public class ConfiguracionJwtTests
{
    private sealed class ApiConJwt(string clave, string valor) : ApiDePrueba
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting(clave, valor);
        }
    }

    [Theory]
    [InlineData("Jwt:Key", "llave_de_31_caracteres_exactos_")]   // 31 bytes
    [InlineData("Jwt:Key", "")]
    [InlineData("Jwt:Issuer", " ")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:ExpirationMinutes", "0")]
    public void No_arranca_con_configuracion_insegura_o_incompleta(string clave, string valor)
    {
        using var api = new ApiConJwt(clave, valor);

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains(clave.Replace("Jwt:", ""), error.ToString());
    }

    [Theory]
    [InlineData("Jwt:Key", "llave_de_32_caracteres_exactos__")]  // justo el mínimo
    [InlineData("Jwt:ExpirationMinutes", "1")]
    public async Task Arranca_con_el_minimo_permitido(string clave, string valor)
    {
        using var api = new ApiConJwt(clave, valor);

        var respuesta = await api.CreateClient().GetAsync("/pruebas/publica");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
