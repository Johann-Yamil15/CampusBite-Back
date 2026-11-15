using CampusBite.Application.Common;
using CampusBite.Application.Ordenes;
using CampusBite.Application.Ordenes.Dtos;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Domain.Ordenes;
using CampusBite.Infrastructure.Persistence;
using static CampusBite.Infrastructure.Persistence.Procedimientos;

namespace CampusBite.Infrastructure.Ordenes;

// JJ-Sprint5 13/11/2026: implementa IOrdenRepository con sp_crear_orden, sp_mis_ordenes, sp_obtener_orden,
// sp_cancelar_orden, sp_cola_cafeteria, sp_cambiar_estado_orden, sp_entregar_orden, sp_confirmar_transferencia
// y sp_crear_reporte_orden.
public class OrdenRepository : IOrdenRepository
{
    private readonly AppDbContext _context;

    public OrdenRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Guid> CrearAsync(Guid idAlumno, Guid idCafeteria, DateTimeOffset recogidaEn, MetodoPago metodo,
        IReadOnlyList<ItemOrdenDto> items, string? notas, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_crear_orden(@alumno, @cafeteria, @recogida, @metodo::metodo_pago, @items, @notas, NULL)",
            [Id("alumno", idAlumno), Id("cafeteria", idCafeteria), Instante("recogida", recogidaEn),
             Enumeracion<MetodoPago>("metodo", metodo),
             Jsonb("items", items.Select(i => new { IdProducto = i.IdProducto, i.Cantidad })),
             Texto("notas", notas)],
            fila => fila.GetGuid(0),
            cancellationToken);

    public async Task<PaginaDto<OrdenDto>> MisOrdenesAsync(Guid idAlumno, int pagina, int tamanoPagina, CancellationToken cancellationToken = default)
    {
        pagina = Math.Max(pagina, 1);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);
        var (items, total) = await _context.EjecutarAsync(
            "CALL sp_mis_ordenes(@alumno, @limite, @desplazamiento, NULL, NULL)",
            [Id("alumno", idAlumno), Entero("limite", tamanoPagina), Entero("desplazamiento", (pagina - 1) * tamanoPagina)],
            fila => (fila.LeerJson<List<OrdenDto>>(0) ?? [], fila.GetInt32(1)),
            cancellationToken);

        return new PaginaDto<OrdenDto> { Items = items, Total = total, Pagina = pagina, TamanoPagina = tamanoPagina };
    }

    public async Task<OrdenDto> ObtenerAsync(Guid idOrden, Guid idActor, CancellationToken cancellationToken = default) =>
        await _context.EjecutarAsync(
            "CALL sp_obtener_orden(@orden, @actor, NULL)",
            [Id("orden", idOrden), Id("actor", idActor)],
            fila => fila.LeerJson<OrdenDto>(0),
            cancellationToken)
        ?? throw new NotFoundException("La orden no existe.");

    public Task CancelarAsync(Guid idOrden, Guid idAlumno, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_cancelar_orden(@orden, @alumno)",
            [Id("orden", idOrden), Id("alumno", idAlumno)],
            cancellationToken);

    public Task<IReadOnlyList<OrdenDto>> TableroAsync(Guid idCafeteria, Guid idActor, DateOnly? fecha, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync<IReadOnlyList<OrdenDto>>(
            "CALL sp_cola_cafeteria(@cafeteria, @actor, @fecha, NULL)",
            [Id("cafeteria", idCafeteria), Id("actor", idActor), Fecha("fecha", fecha)],
            fila => fila.LeerJson<List<OrdenDto>>(0) ?? [],
            cancellationToken);

    public Task CambiarEstadoAsync(Guid idOrden, EstadoOrden estado, Guid idActor, string? motivo, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_cambiar_estado_orden(@orden, @estado::estado_orden, @actor, @motivo)",
            [Id("orden", idOrden), Enumeracion<EstadoOrden>("estado", estado), Id("actor", idActor), Texto("motivo", motivo)],
            cancellationToken);

    public async Task EntregarAsync(Guid idOrden, string codigo, Guid idActor, CancellationToken cancellationToken = default)
    {
        var resultado = await _context.EjecutarAsync(
            "CALL sp_entregar_orden(@orden, @codigo, @actor, NULL)",
            [Id("orden", idOrden), Texto("codigo", codigo), Id("actor", idActor)],
            fila => fila.GetString(0),
            cancellationToken);

        switch (resultado)
        {
            case "codigo_incorrecto":
                throw new BusinessRuleException("Código de recogida incorrecto");
            case "bloqueada":
                throw new TooManyRequestsException("Demasiados códigos incorrectos para esta orden. Intenta de nuevo en 10 minutos.");
        }
    }

    public Task ConfirmarPagoAsync(Guid idOrden, Guid idActor, string? referencia, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_confirmar_transferencia(@orden, @actor, @referencia)",
            [Id("orden", idOrden), Id("actor", idActor), Texto("referencia", referencia)],
            cancellationToken);

    public Task<string> ReportarAsync(Guid idOrden, Guid idAlumno, MotivoReporte motivo, string? detalle, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_crear_reporte_orden(@orden, @alumno, @motivo::motivo_reporte, @detalle, NULL)",
            [Id("orden", idOrden), Id("alumno", idAlumno), Enumeracion<MotivoReporte>("motivo", motivo), Texto("detalle", detalle)],
            fila => fila.GetString(0),
            cancellationToken);
}
