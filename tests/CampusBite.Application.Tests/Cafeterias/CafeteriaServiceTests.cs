using CampusBite.Application.Cafeterias;
using CampusBite.Application.Cafeterias.Dtos;
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
        await Assert.ThrowsAsync<NotFoundException>(() => _servicio.ObtenerAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(RolUsuario.Alumno)]
    [InlineData(RolUsuario.AdminCafeteria)]
    public async Task Listar_Inactivas_SinSerAdminSistema_LanzaForbidden_SinIrALaBD(RolUsuario rol)
    {
        _usuario.Rol = rol;

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _servicio.ListarAsync(new ConsultaCafeteriasDto { IncluirInactivas = true }));
        Assert.Equal(0, _repositorio.Llamadas);
    }

    [Fact]
    public async Task Listar_Activas_CualquierRol_ConsultaConElActor_YLaPrimeraPaginaDe20()
    {
        _usuario.Rol = RolUsuario.Alumno;

        await _servicio.ListarAsync(new ConsultaCafeteriasDto());

        Assert.Equal((_usuario.Id, false, (string?)null, 20, 0), _repositorio.UltimoListado);
    }

    // JY-Sprint4 31/10/2026: paginación del listado
    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(76, 20, 1500)]
    [InlineData(3, 100, 200)]
    public async Task Listar_ConvierteLaPaginaEnDesplazamiento(int pagina, int tamano, int desplazamiento)
    {
        await _servicio.ListarAsync(new ConsultaCafeteriasDto { Pagina = pagina, TamanoPagina = tamano });

        Assert.Equal((tamano, desplazamiento), (_repositorio.UltimoListado!.Value.Limite, _repositorio.UltimoListado.Value.Desplazamiento));
    }

    [Theory]
    [InlineData("  Central ", "Central")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public async Task Listar_RecortaLaBusqueda_YLaVaciaEsTodas(string? enviada, string? consultada)
    {
        await _servicio.ListarAsync(new ConsultaCafeteriasDto { Buscar = enviada });

        Assert.Equal(consultada, _repositorio.UltimoListado!.Value.Buscar);
    }

    [Fact]
    public async Task Listar_Inactivas_ComoAdminSistema_LasPide()
    {
        _usuario.Rol = RolUsuario.AdminSistema;

        await _servicio.ListarAsync(new ConsultaCafeteriasDto { IncluirInactivas = true });

        Assert.True(_repositorio.UltimoListado!.Value.Inactivas);
    }

    [Fact]
    public async Task Listar_DevuelveLaPaginaConElTotalDeLaBD()
    {
        _repositorio.Agregar("Cafetería Central");
        _repositorio.TotalSimulado = 1534;

        var pagina = await _servicio.ListarAsync(new ConsultaCafeteriasDto { Pagina = 3, TamanoPagina = 50 });

        Assert.Equal((1534, 3, 50), (pagina.Total, pagina.Pagina, pagina.TamanoPagina));
        Assert.Equal("Cafetería Central", Assert.Single(pagina.Items).Nombre);
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

    public Task<(int Total, IReadOnlyList<CafeteriaDto> Items)> ListarAsync(
        Guid idActor, bool incluirInactivas, string? buscar, int limite, int desplazamiento,
        CancellationToken cancellationToken = default)
    {
        Registrar();
        UltimoListado = (idActor, incluirInactivas, buscar, limite, desplazamiento);
        return Task.FromResult<(int, IReadOnlyList<CafeteriaDto>)>((TotalSimulado, _cafeterias.Values.ToList()));
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
