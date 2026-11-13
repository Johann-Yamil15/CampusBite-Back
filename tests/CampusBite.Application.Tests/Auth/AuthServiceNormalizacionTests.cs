using CampusBite.Application.Auth;
using CampusBite.Application.Auth.Dtos;
using CampusBite.Application.Tests.Productos;
using CampusBite.Domain.Cafeterias;

namespace CampusBite.Application.Tests.Auth;

// Normalización de los datos opcionales del registro antes de llegar al repositorio (y de ahí a la BD):
// matrícula y teléfono en blanco viajan como NULL; con valor, recortados. Completa las ramas que AuthServiceTests no
// cubría (matrícula con valor, teléfono en blanco y el teléfono del registro de negocio).
public class AuthServiceNormalizacionTests
{
    private readonly FakeUsuarioRepository _repositorio = new();
    private readonly AuthService _servicio;

    public AuthServiceNormalizacionTests()
    {
        _servicio = new AuthService(_repositorio, new FakeTokenService(), new FakePasswordHasher(), new FakeEnviadorCorreo(),
            new FakeLogger<AuthService>());
    }

    [Fact]
    public async Task Registro_con_matricula_la_recorta_y_telefono_en_blanco_viaja_como_null()
    {
        await _servicio.RegistrarAsync(new RegistroRequestDto
        {
            Nombre = "Beto", Correo = "beto@uttt.edu.mx", Password = "Password123", Matricula = "  UTT1234567 ", Telefono = "   "
        });

        var registro = _repositorio.UltimoRegistro!.Value;
        Assert.Equal("UTT1234567", registro.Matricula);
        Assert.Null(registro.Telefono);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(" 7711234567 ", "7711234567")]
    public async Task Registro_de_negocio_normaliza_el_telefono(string? telefono, string? esperado)
    {
        await _servicio.RegistrarCafeteriaAsync(new RegistroCafeteriaRequestDto
        {
            NombreEncargado = " Ana ", Correo = "ana@uttt.edu.mx", Password = "Password123", Telefono = telefono,
            NombreCafeteria = " Café ", Ubicacion = " B ", EsDelCampus = true, PuntoEntrega = " V ",
            MetodosPago = [MetodoPago.Efectivo, MetodoPago.Efectivo, MetodoPago.Tarjeta]
        });

        var r = _repositorio.UltimoRegistroCafeteria!.Value;
        Assert.Equal(esperado, r.Telefono);
        Assert.Equal(("Ana", "Café", "B", "V"), (r.NombreEncargado, r.NombreCafeteria, r.Ubicacion, r.PuntoEntrega));
        Assert.Equal([MetodoPago.Efectivo, MetodoPago.Tarjeta], r.Metodos);           // sin repetidos
    }
}
