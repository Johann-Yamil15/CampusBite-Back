using CampusBite.Application.Cafeterias;
using CampusBite.Application.Cafeterias.Dtos;
using CampusBite.Application.Common.Dtos;
using CampusBite.Application.Common.Security;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;

namespace CampusBite.Application.Tests.Cafeterias;

// JY-Sprint3 20/10/2026: TEC-08. Reglas de CafeteriaService con un repositorio y una sesión falsos
public class CafeteriaServiceTests
{
    private readonly FakeCafeteriaRepository _repositorio = new();
    private readonly FakeUsuarioActual _usuario = new() { Rol = RolUsuario.AdminSistema };
    private readonly CafeteriaService _servicio;

    public CafeteriaServiceTests()
    {
        _servicio = new CafeteriaService(_repositorio, _usuario);
    }

    private static GuardarCafeteriaDto Datos() => new()
    {
        Nombre = "  Cafetería Norte ", Ubicacion = " Edificio K ", EsDelCampus = true, PuntoEntrega = " Ventanilla 2 ",
        MetodosPago = [MetodoPago.Efectivo, MetodoPago.Tarjeta, MetodoPago.Efectivo]
    };

    [Fact]
    public async Task Crear_NormalizaDatos_UsaElActorDeLaSesion_YDevuelveLaCreada()
    {
        var creada = await _servicio.CrearAsync(Datos());

        var (actor, datos) = _repositorio.UltimaCreacion!.Value;
        Assert.Equal(_usuario.Id, actor);
        Assert.Equal("Cafetería Norte", datos.Nombre);
        Assert.Equal("Edificio K", datos.Ubicacion);
        Assert.Equal("Ventanilla 2", datos.PuntoEntrega);
        Assert.Equal([MetodoPago.Tarjeta, MetodoPago.Efectivo], datos.MetodosPago);   // sin repetidos, orden estable
        Assert.Equal("Cafetería Norte", creada.Nombre);
    }

    [Fact]
    public async Task Actualizar_PasaElIdYElActor_YDevuelveLaVersionNueva()
    {
        var id = _repositorio.Agregar("Vieja");

        var actualizada = await _servicio.ActualizarAsync(id, Datos());

        Assert.Equal((id, _usuario.Id), _repositorio.UltimaActualizacion);
        Assert.Equal("Cafetería Norte", actualizada.Nombre);
    }

    [Fact]
    public async Task Obtener_SiNoExisteOEstaOculta_LanzaNotFound()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() => _servicio.ObtenerAsync(Guid.NewGuid()));
        Assert.Equal("La cafetería no existe.", error.Message);   // el front muestra este texto
    }

    [Theory]
    [InlineData(RolUsuario.Alumno)]
    [InlineData(RolUsuario.AdminCafeteria)]
    public async Task Listar_Inactivas_SinSerAdminSistema_LanzaForbidden_SinIrALaBD(RolUsuario rol)
    {
        _usuario.Rol = rol;

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => _servicio.ListarAsync(new ListarCafeteriasDto { IncluirInactivas = true }));
        Assert.Equal("Solo el administrador del sistema puede ver cafeterías inactivas.", error.Message);
        Assert.Equal(0, _repositorio.Llamadas);
    }

    [Fact]
    public async Task Listar_Activas_CualquierRol_ConsultaConElActor()
    {
        _usuario.Rol = RolUsuario.Alumno;

        await _servicio.ListarAsync(new ListarCafeteriasDto());

        Assert.Equal((_usuario.Id, false, (string?)null, 20, 0), _repositorio.UltimoListado);
    }

    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(3, 20, 40)]
    [InlineData(2, 100, 100)]
    public async Task Listar_ConviertePaginaEnDesplazamiento(int pagina, int tamano, int desplazamiento)
    {
        await _servicio.ListarAsync(new ListarCafeteriasDto { Pagina = pagina, Tamano = tamano });

        Assert.Equal(tamano, _repositorio.UltimoListado!.Value.Limite);
        Assert.Equal(desplazamiento, _repositorio.UltimoListado!.Value.Desplazamiento);
    }

    [Theory]
    [InlineData("  norte ", "norte")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public async Task Listar_LimpiaLaBusqueda(string? buscar, string? esperado)
    {
        await _servicio.ListarAsync(new ListarCafeteriasDto { Buscar = buscar });

        Assert.Equal(esperado, _repositorio.UltimoListado!.Value.Buscar);
    }

    [Fact]
    public async Task Listar_ArmaLaPaginaConElTotalDeLaBD()
    {
        _repositorio.Agregar("A");
        _repositorio.Agregar("B");
        _repositorio.TotalSimulado = 45;

        var pagina = await _servicio.ListarAsync(new ListarCafeteriasDto { Pagina = 2, Tamano = 20 });

        Assert.Equal(2, pagina.Elementos.Count);
        Assert.Equal(45, pagina.Total);
        Assert.Equal(2, pagina.Pagina);
        Assert.Equal(20, pagina.Tamano);
        Assert.Equal(3, pagina.TotalPaginas);
    }

    [Fact]
    public async Task Desactivar_Y_Reactivar_MandanElEstadoCorrecto()
    {
        var id = Guid.NewGuid();

        await _servicio.DesactivarAsync(id);
        Assert.Equal((id, false, _usuario.Id), _repositorio.UltimoCambioActiva);

        await _servicio.ReactivarAsync(id);
        Assert.Equal((id, true, _usuario.Id), _repositorio.UltimoCambioActiva);
    }

    [Fact]
    public async Task Crear_ErrorDeLaBD_SePropaga()
    {
        _repositorio.Error = new ConflictException("Ya existe una cafetería con el nombre \"Cafetería Norte\"");

        await Assert.ThrowsAsync<ConflictException>(() => _servicio.CrearAsync(Datos()));
    }
}

public class FakeUsuarioActual : IUsuarioActual
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public RolUsuario Rol { get; set; }
}

public class FakeCafeteriaRepository : ICafeteriaRepository
{
    private readonly Dictionary<Guid, CafeteriaDto> _cafeterias = [];

    public Exception? Error { get; set; }
    public int Llamadas { get; private set; }
    public (Guid Actor, DatosCafeteria Datos)? UltimaCreacion { get; private set; }
    public (Guid Id, Guid Actor)? UltimaActualizacion { get; private set; }
    public (Guid Actor, bool Inactivas, string? Buscar, int Limite, int Desplazamiento)? UltimoListado { get; private set; }
    public int TotalSimulado { get; set; }
    public (Guid Id, bool Activa, Guid Actor)? UltimoCambioActiva { get; private set; }

    public Guid Agregar(string nombre)
    {
        var id = Guid.NewGuid();
        _cafeterias[id] = new CafeteriaDto { IdCafeteria = id, Nombre = nombre, Activa = true };
        return id;
    }

    public Task<(IReadOnlyList<CafeteriaDto> Cafeterias, int Total)> ListarAsync(
        Guid idActor, bool incluirInactivas, string? buscar, int limite, int desplazamiento, CancellationToken cancellationToken = default)
    {
        Registrar();
        UltimoListado = (idActor, incluirInactivas, buscar, limite, desplazamiento);
        return Task.FromResult<(IReadOnlyList<CafeteriaDto>, int)>((_cafeterias.Values.ToList(), TotalSimulado));
    }

    public Task<CafeteriaDto?> ObtenerAsync(Guid idCafeteria, Guid idActor, CancellationToken cancellationToken = default)
    {
        Registrar();
        return Task.FromResult(_cafeterias.GetValueOrDefault(idCafeteria));
    }

    public Task<Guid> CrearAsync(Guid idActor, DatosCafeteria datos, CancellationToken cancellationToken = default)
    {
        Registrar();
        UltimaCreacion = (idActor, datos);
        var id = Agregar(datos.Nombre);
        return Task.FromResult(id);
    }

    public Task ActualizarAsync(Guid idCafeteria, Guid idActor, DatosCafeteria datos, CancellationToken cancellationToken = default)
    {
        Registrar();
        UltimaActualizacion = (idCafeteria, idActor);
        _cafeterias[idCafeteria] = new CafeteriaDto { IdCafeteria = idCafeteria, Nombre = datos.Nombre, Activa = true };
        return Task.CompletedTask;
    }

    public Task CambiarActivaAsync(Guid idCafeteria, bool activa, Guid idActor, CancellationToken cancellationToken = default)
    {
        Registrar();
        UltimoCambioActiva = (idCafeteria, activa, idActor);
        return Task.CompletedTask;
    }

    private void Registrar()
    {
        Llamadas++;
        if (Error is not null) throw Error;
    }
}
