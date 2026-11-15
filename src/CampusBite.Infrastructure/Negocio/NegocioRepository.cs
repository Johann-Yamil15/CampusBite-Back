using CampusBite.Application.Negocio;
using CampusBite.Domain.Promociones;
using CampusBite.Infrastructure.Persistence;
using static CampusBite.Infrastructure.Persistence.Procedimientos;

namespace CampusBite.Infrastructure.Negocio;

// JJ-Sprint5 13/11/2026: implementa INegocioRepository con los sp_* de horario (037), promociones (038) y
// estadísticas (039)
public class NegocioRepository : INegocioRepository
{
    private readonly AppDbContext _context;

    public NegocioRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HorarioDto> ObtenerHorarioAsync(Guid idCafeteria, Guid idActor, CancellationToken cancellationToken = default) =>
        (await _context.EjecutarAsync(
            "CALL sp_obtener_horario(@cafeteria, @actor, NULL)",
            [Id("cafeteria", idCafeteria), Id("actor", idActor)],
            fila => fila.LeerJson<HorarioDto>(0),
            cancellationToken))!;

    public Task GuardarHorarioAsync(Guid idCafeteria, Guid idActor, IReadOnlyList<DiaHorarioDto> dias, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_guardar_horario(@cafeteria, @actor, @dias)",
            [Id("cafeteria", idCafeteria), Id("actor", idActor), Jsonb("dias", dias)],
            cancellationToken);

    public Task CambiarCierreHoyAsync(Guid idCafeteria, Guid idActor, bool cerrar, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_cambiar_cierre_hoy(@cafeteria, @actor, @cerrar)",
            [Id("cafeteria", idCafeteria), Id("actor", idActor), Logico("cerrar", cerrar)],
            cancellationToken);

    public Task<IReadOnlyList<PromocionDto>> ListarPromocionesAsync(Guid idCafeteria, Guid idActor, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync<IReadOnlyList<PromocionDto>>(
            "CALL sp_listar_promociones(@cafeteria, @actor, NULL)",
            [Id("cafeteria", idCafeteria), Id("actor", idActor)],
            fila => fila.LeerJson<List<PromocionDto>>(0) ?? [],
            cancellationToken);

    public async Task<PromocionDto> GuardarPromocionAsync(Guid idCafeteria, Guid idActor, Guid? idPromocion, GuardarPromocionDto dto,
        CancellationToken cancellationToken = default) =>
        (await _context.EjecutarAsync(
            "CALL sp_guardar_promocion(@cafeteria, @actor, @id, @nombre, @descripcion, @tipo::tipo_promocion, @valor, " +
            "@desde, @hasta, @activa, NULL)",
            [Id("cafeteria", idCafeteria), Id("actor", idActor), IdOpcional("id", idPromocion), Texto("nombre", dto.Nombre.Trim()),
             Texto("descripcion", dto.Descripcion?.Trim()), Enumeracion<TipoPromocion>("tipo", dto.Tipo), Numero("valor", dto.Valor),
             Fecha("desde", dto.Desde), Fecha("hasta", dto.Hasta), Logico("activa", dto.Activa)],
            fila => fila.LeerJson<PromocionDto>(0),
            cancellationToken))!;

    public Task EliminarPromocionAsync(Guid idCafeteria, Guid idActor, Guid idPromocion, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_eliminar_promocion(@cafeteria, @actor, @id)",
            [Id("cafeteria", idCafeteria), Id("actor", idActor), Id("id", idPromocion)],
            cancellationToken);

    public Task<IReadOnlyList<PromocionActivaDto>> PromocionesActivasAsync(CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync<IReadOnlyList<PromocionActivaDto>>(
            "CALL sp_promociones_activas(NULL)",
            [],
            fila => fila.LeerJson<List<PromocionActivaDto>>(0) ?? [],
            cancellationToken);

    public async Task<EstadisticasDto> EstadisticasAsync(Guid idCafeteria, Guid idActor, string periodo, CancellationToken cancellationToken = default) =>
        (await _context.EjecutarAsync(
            "CALL sp_estadisticas_cafeteria(@cafeteria, @actor, @periodo, NULL)",
            [Id("cafeteria", idCafeteria), Id("actor", idActor), Texto("periodo", periodo)],
            fila => fila.LeerJson<EstadisticasDto>(0),
            cancellationToken))!;
}
