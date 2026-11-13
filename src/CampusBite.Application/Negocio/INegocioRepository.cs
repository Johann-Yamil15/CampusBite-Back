namespace CampusBite.Application.Negocio;

// JJ-Sprint5 13/11/2026: horario, cierre de hoy, promociones y estadísticas de una cafetería.
// Solo su encargado o admin_sistema (lo revisa la BD con idActor). shortcut: sin capa de servicio, como Menu.
public interface INegocioRepository
{
    Task<HorarioDto> ObtenerHorarioAsync(Guid idCafeteria, Guid idActor, CancellationToken cancellationToken = default);
    Task GuardarHorarioAsync(Guid idCafeteria, Guid idActor, IReadOnlyList<DiaHorarioDto> dias, CancellationToken cancellationToken = default);
    Task CambiarCierreHoyAsync(Guid idCafeteria, Guid idActor, bool cerrar, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromocionDto>> ListarPromocionesAsync(Guid idCafeteria, Guid idActor, CancellationToken cancellationToken = default);

    // idPromocion null = crear
    Task<PromocionDto> GuardarPromocionAsync(Guid idCafeteria, Guid idActor, Guid? idPromocion, GuardarPromocionDto dto, CancellationToken cancellationToken = default);
    Task EliminarPromocionAsync(Guid idCafeteria, Guid idActor, Guid idPromocion, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromocionActivaDto>> PromocionesActivasAsync(CancellationToken cancellationToken = default);

    Task<EstadisticasDto> EstadisticasAsync(Guid idCafeteria, Guid idActor, string periodo, CancellationToken cancellationToken = default);
}
