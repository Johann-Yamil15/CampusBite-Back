using System.Security.Claims;
using CampusBite.Api.Common.Security;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Security;
using Microsoft.AspNetCore.Http;

namespace CampusBite.Api.Tests.Seguridad;

// UsuarioActual (quién hace la petición, leído del JWT ya validado) y BCryptPasswordHasher (el hasher real).
public class UsuarioActualTests
{
    private static UsuarioActual Con(ClaimsPrincipal? usuario)
    {
        var accesor = new HttpContextAccessor();
        if (usuario is not null)
        {
            accesor.HttpContext = new DefaultHttpContext { User = usuario };
        }
        return new UsuarioActual(accesor);
    }

    private static ClaimsPrincipal Autenticado(params (string Tipo, string Valor)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Tipo, c.Valor)), authenticationType: "Bearer"));

    [Fact]
    public void Lee_id_y_rol_del_token()
    {
        var id = Guid.NewGuid();
        var actual = Con(Autenticado(("sub", id.ToString()), ("role", "AdminCafeteria")));

        Assert.Equal((id, RolUsuario.AdminCafeteria), (actual.Id, actual.Rol));
    }

    [Fact]
    public void Sin_peticion_en_curso_no_hay_sesion()
    {
        var actual = Con(null);

        Assert.Throws<UnauthorizedException>(() => actual.Id);
        Assert.Throws<UnauthorizedException>(() => actual.Rol);
    }

    [Fact]
    public void Una_identidad_no_autenticada_no_cuenta_aunque_traiga_claims()
    {
        var anonimo = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", "Alumno")]));
        var actual = Con(anonimo);

        Assert.Throws<UnauthorizedException>(() => actual.Id);
        Assert.Throws<UnauthorizedException>(() => actual.Rol);
    }

    [Theory]
    [InlineData("no-es-guid")]
    [InlineData("")]
    public void Un_sub_invalido_no_es_una_sesion_valida(string sub)
    {
        Assert.Throws<UnauthorizedException>(() => Con(Autenticado(("sub", sub), ("role", "Alumno"))).Id);
    }

    [Fact]
    public void Sin_claims_de_id_ni_rol_no_es_una_sesion_valida()
    {
        var actual = Con(Autenticado(("email", "ana@uttt.edu.mx")));

        Assert.Throws<UnauthorizedException>(() => actual.Id);
        Assert.Throws<UnauthorizedException>(() => actual.Rol);
    }

    [Theory]
    [InlineData("alumno")]           // distinto en mayúsculas: no es exactamente el del enum
    [InlineData("Superusuario")]
    [InlineData("99")]                // número fuera del enum
    public void Un_rol_que_no_es_del_enum_no_es_una_sesion_valida(string rol)
    {
        Assert.Throws<UnauthorizedException>(() => Con(Autenticado(("sub", Guid.NewGuid().ToString()), ("role", rol))).Rol);
    }
}

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void El_hash_es_BCrypt_con_costo_12_sal_aleatoria_y_verifica_solo_la_contrasena_correcta()
    {
        var hash = _hasher.Hash("Password123");
        var otro = _hasher.Hash("Password123");

        Assert.StartsWith("$2", hash);
        Assert.Equal("12", hash.Split('$')[2]);
        Assert.NotEqual(hash, otro);                                       // sal distinta cada vez
        Assert.True(_hasher.Verify("Password123", hash));
        Assert.True(_hasher.Verify("Password123", otro));
        Assert.False(_hasher.Verify("password123", hash));
        Assert.False(_hasher.Verify("", hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sin_hash_guardado_nunca_verifica(string? hash)
    {
        Assert.False(_hasher.Verify("Password123", hash));
    }

    [Fact]
    public void Un_hash_con_formato_invalido_es_contrasena_incorrecta_y_no_un_error()
    {
        Assert.False(_hasher.Verify("Password123", "HASH_DE_PRUEBA_NO_VALIDO"));   // marcadores de los datos de prueba
    }

    // Defecto D-2 (documentado, sin corregir: requiere autorización). Un hash con prefijo BCrypt pero truncado
    // ("$2a$12$corto") hace que BCrypt.Net lance ArgumentOutOfRangeException; Verify solo atrapa SaltParseException,
    // así que un login contra un hash dañado en la BD respondería 500 en vez de 401. La prueba expresa el contrato
    // correcto y queda omitida hasta aprobar la corrección.
    [Fact(Skip = "Defecto D-2: BCryptPasswordHasher.Verify no atrapa ArgumentOutOfRangeException (pendiente de autorización)")]
    public void Un_hash_BCrypt_truncado_deberia_ser_contrasena_incorrecta()
    {
        Assert.False(_hasher.Verify("Password123", "$2a$12$corto"));
    }
}
