using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Ordenes;

namespace CampusBite.Application.Common.Correo;

// JY-Sprint5 27/11/2026: seguimiento del pedido por correo. Cada cambio de estado del pedido manda un aviso al alumno,
// aunque no esté en la página: lo que ve en pantalla (el front consulta cada 5 s) le llega también a su bandeja.

/// <summary>Qué pasó con el pedido (de ahí salen el asunto y el texto del correo).</summary>
public enum EventoPedido
{
    /// <summary>Se creó la orden (queda confirmada, o pendiente si falta confirmar el pago).</summary>
    Creada = 1,
    /// <summary>La cafetería confirmó el pago (transferencia o tarjeta).</summary>
    PagoConfirmado = 2,
    EnPreparacion = 3,
    Lista = 4,
    Entregada = 5,
    /// <summary>La cancelaron el alumno o la cafetería (lleva el motivo cuando existe).</summary>
    Cancelada = 6
}

public sealed record LineaAvisoPedido(string Producto, int Cantidad, decimal PrecioUnitario);

/// <summary>Datos de la orden que lleva el correo. El código de recogida solo viaja en el aviso de "lista".</summary>
public sealed record AvisoPedido(
    EventoPedido Evento,
    Guid IdOrden,
    string Cafeteria,
    string PuntoEntrega,
    DateTimeOffset RecogidaEn,
    decimal Total,
    MetodoPago Metodo,
    EstadoPago EstadoPago,
    IReadOnlyList<LineaAvisoPedido> Lineas,
    string? CodigoRecogida,
    string? MotivoCancelacion);
