using CampusBite.Application.Common.Correo;
using CampusBite.Application.Ordenes.Dtos;
using Microsoft.Extensions.Logging;

namespace CampusBite.Application.Ordenes;

// JY-Sprint5 27/11/2026: avisa por correo al dueño de la orden cuando cambia su estado (creada, pago confirmado, en
// preparación, lista, entregada, cancelada). Se reutiliza el mismo SMTP de la verificación de cuenta (IEnviadorCorreo).
//
// Reglas:
//  - Un aviso NUNCA hace fallar la operación sobre la orden: si no se puede leer el destinatario o el SMTP falla, solo se
//    registra. El pedido ya cambió de estado y eso no se puede deshacer por un correo.
//  - El envío no espera al SMTP (puede tardar hasta su tiempo límite): el encargado y el alumno reciben su respuesta de
//    inmediato. Solo se usan datos ya leídos y el IEnviadorCorreo (singleton); nada del request (DbContext) cruza al
//    envío, que corre después de que la petición terminó.
//  - Si el alumno apagó "avisarme cuando mi pedido esté listo" (avisoPedidoListo), no se le manda ese correo.
//  - El log nunca lleva el correo ni el código de recogida: solo el id de la orden, el evento y el tipo de error.
public interface INotificadorPedido
{
    /// <param name="orden">La orden ya actualizada (la que la API responde).</param>
    /// <param name="idActor">Quien hizo el cambio (el alumno o el encargado): la BD revisa que pueda ver la orden.</param>
    Task NotificarAsync(EventoPedido evento, OrdenDto orden, Guid idActor, CancellationToken cancellationToken = default);
}

public sealed class NotificadorPedido : INotificadorPedido
{
    private readonly IOrdenRepository _ordenes;
    private readonly IEnviadorCorreo _correo;
    private readonly ILogger<NotificadorPedido> _logger;

    public NotificadorPedido(IOrdenRepository ordenes, IEnviadorCorreo correo, ILogger<NotificadorPedido> logger)
    {
        _ordenes = ordenes;
        _correo = correo;
        _logger = logger;
    }

    public async Task NotificarAsync(EventoPedido evento, OrdenDto orden, Guid idActor, CancellationToken cancellationToken = default)
    {
        DestinatarioOrden? destinatario;
        try
        {
            destinatario = await _ordenes.DestinatarioAsync(orden.IdOrden, idActor, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("No se pudo leer el destinatario del aviso {Evento} de la orden {IdOrden} ({Tipo})",
                evento, orden.IdOrden, ex.GetType().Name);
            return;
        }

        if (destinatario is null)
        {
            _logger.LogInformation("La orden {IdOrden} no tiene a quién avisar ({Evento}): sin correo verificado o inactivo",
                orden.IdOrden, evento);
            return;
        }

        if (evento == EventoPedido.Lista && !destinatario.AvisoPedidoListo)
        {
            _logger.LogInformation("La orden {IdOrden} no se avisa como lista: el alumno apagó ese aviso", orden.IdOrden);
            return;
        }

        var aviso = Construir(evento, orden, destinatario);
        _ = EnviarSinEsperarAsync(destinatario.Correo, destinatario.Nombre, aviso);
    }

    private static AvisoPedido Construir(EventoPedido evento, OrdenDto orden, DestinatarioOrden destinatario) => new(
        evento, orden.IdOrden, orden.Cafeteria, orden.PuntoEntrega, orden.RecogidaEn, orden.Total, orden.Metodo,
        orden.EstadoPago,
        orden.Lineas.Select(l => new LineaAvisoPedido(l.Producto, l.Cantidad, l.PrecioUnitario)).ToList(),
        evento == EventoPedido.Lista ? destinatario.CodigoRecogida : null,
        evento == EventoPedido.Cancelada ? orden.MotivoCancelacion : null);

    // Sin CancellationToken de la petición: el envío debe terminar aunque el cliente ya recibió su respuesta
    private async Task EnviarSinEsperarAsync(string correo, string nombre, AvisoPedido aviso)
    {
        try
        {
            await _correo.EnviarEstadoPedidoAsync(correo, nombre, aviso, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("No se pudo enviar el aviso {Evento} de la orden {IdOrden} ({Tipo})",
                aviso.Evento, aviso.IdOrden, ex.GetType().Name);
        }
    }
}
