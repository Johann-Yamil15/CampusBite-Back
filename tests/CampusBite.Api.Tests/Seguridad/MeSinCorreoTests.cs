using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace CampusBite.Api.Tests.Seguridad;

// GET /api/auth/me con un token sin claim de correo (la rama "correo ausente")
public class MeSinCorreoTests(ApiDePrueba api) : IClassFixture<ApiDePrueba>
{
    [Fact]
    public async Task Me_con_un_token_sin_correo_devuelve_correo_null_y_los_datos_de_la_sesion()
    {
        var token = ApiDePrueba.TokenManual("Alumno", DateTime.UtcNow.AddHours(1));

        var me = (await (await api.ClienteConToken(token).GetAsync("/api/auth/me")).Content.ReadFromJsonAsync<JsonObject>())!;

        Assert.Null((string?)me["correo"]);
        Assert.Equal("Alumno", (string?)me["rol"]);
        Assert.True(Guid.TryParse((string?)me["id"], out _));
    }
}
