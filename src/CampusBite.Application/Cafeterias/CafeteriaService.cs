using CampusBite.Application.Cafeterias.Dtos;
using CampusBite.Application.Common.Security;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Common.Exceptions;

namespace CampusBite.Application.Cafeterias;

// JY-Sprint3 20/10/2026: TEC-08. Reglas del CRUD de cafeterías. Los permisos finos (¿es encargado de ESTA
// cafetería?) los revisa la BD; aquí se limpia la entrada y se arma la respuesta.
public class CafeteriaService : ICafeteriaService
{
    private readonly ICafeteriaRepository _cafeterias;
    private readonly IUsuarioActual _usuario;

    public CafeteriaService(ICafeteriaRepository cafeterias, IUsuarioActual usuario)
    {
        _cafeterias = cafeterias;
        _usuario = usuario;
    }

    public Task<IReadOnlyList<CafeteriaDto>> ListarAsync(bool incluirInactivas, CancellationToken cancellationToken = default)
    {
        // Se corta aquí para no ir a la BD con una petición que de todos modos va a negar
        if (incluirInactivas && _usuario.Rol != RolUsuario.AdminSistema)
        {
            throw new ForbiddenException("Solo el administrador del sistema puede ver cafeterías inactivas.");
        }

        return _cafeterias.ListarAsync(_usuario.Id, incluirInactivas, cancellationToken);
    }

    public async Task<CafeteriaDto> ObtenerAsync(Guid idCafeteria, CancellationToken cancellationToken = default)
    {
        return await _cafeterias.ObtenerAsync(idCafeteria, _usuario.Id, cancellationToken)
               ?? throw new NotFoundException("La cafetería no existe.");
    }

    public async Task<CafeteriaDto> CrearAsync(GuardarCafeteriaDto dto, CancellationToken cancellationToken = default)
    {
        var id = await _cafeterias.CrearAsync(_usuario.Id, Normalizar(dto), cancellationToken);
        return await ObtenerAsync(id, cancellationToken);
    }

    public async Task<CafeteriaDto> ActualizarAsync(Guid idCafeteria, GuardarCafeteriaDto dto, CancellationToken cancellationToken = default)
    {
        await _cafeterias.ActualizarAsync(idCafeteria, _usuario.Id, Normalizar(dto), cancellationToken);
        return await ObtenerAsync(idCafeteria, cancellationToken);
    }

    public Task DesactivarAsync(Guid idCafeteria, CancellationToken cancellationToken = default) =>
        _cafeterias.CambiarActivaAsync(idCafeteria, activa: false, _usuario.Id, cancellationToken);

    public Task ReactivarAsync(Guid idCafeteria, CancellationToken cancellationToken = default) =>
        _cafeterias.CambiarActivaAsync(idCafeteria, activa: true, _usuario.Id, cancellationToken);

    // Recorta espacios y quita métodos repetidos, en un orden estable
    private static DatosCafeteria Normalizar(GuardarCafeteriaDto dto) => new(
        dto.Nombre.Trim(),
        dto.Ubicacion.Trim(),
        dto.EsDelCampus!.Value,
        dto.PuntoEntrega.Trim(),
        dto.MetodosPago.Distinct().Order().ToList());
}
