using CampusBite.Application.Auth;
using CampusBite.Application.Auth.Dtos;
using CampusBite.Application.Tests.Productos;

namespace CampusBite.Application.Tests.Auth;

// El token de verificación es válido pero el usuario ya no se puede leer por su id (p. ej. se borró justo después de
// verificar): la sesión se crea igual con los datos que devolvió la verificación, sin nombre ni cafetería.
public class VerificarCorreoSinUsuarioTests
{
    // Mismo repositorio de prueba, pero ObtenerPorIdAsync no encuentra a nadie (se reimplementa la interfaz)
    private sealed class RepositorioSinLecturaPorId : FakeUsuarioRepository, IUsuarioRepository
    {
        Task<CredencialesUsuario?> IUsuarioRepository.ObtenerPorIdAsync(Guid idUsuario, CancellationToken cancellationToken) =>
            Task.FromResult<CredencialesUsuario?>(null);
    }

    [Fact]
    public async Task VerificarCorreo_SiElUsuarioNoSeEncuentraPorId_IniciaSesionConNombreVacioYSinCafeteria()
    {
        var repositorio = new RepositorioSinLecturaPorId();
        var correo = new FakeEnviadorCorreo();
        var servicio = new AuthService(repositorio, new FakeTokenService(), new FakePasswordHasher(), correo,
            new FakeLogger<AuthService>());
        await servicio.RegistrarAsync(new RegistroRequestDto
        {
            Nombre = "Ana López", Correo = "ana@uttt.edu.mx", Password = "Password123"
        });

        var sesion = await servicio.VerificarCorreoAsync(new VerificarCorreoRequestDto { Token = correo.Enviados[0].Token });

        Assert.Equal("ana@uttt.edu.mx", sesion.Correo);
        Assert.Equal(string.Empty, sesion.Nombre);
        Assert.Null(sesion.IdCafeteria);
        Assert.StartsWith("token:", sesion.Token);
    }
}
