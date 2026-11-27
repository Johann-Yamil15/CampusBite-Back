using System.Globalization;
using System.Net;
using System.Text;
using CampusBite.Application.Common.Correo;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Ordenes;

namespace CampusBite.Infrastructure.Correo;

// JY-Sprint5 27/11/2026: asunto y cuerpo (HTML y texto) de los avisos de seguimiento del pedido.
// Todo lo que viene de la base (nombre de la cafetería, productos, motivo) se escapa en el HTML: no puede inyectar
// marcas en el correo. El asunto no lleva saltos de línea (un asunto con CR/LF no es válido y abriría un encabezado).
internal static class PlantillaPedido
{
    // Los horarios de las cafeterías son del centro de México, que ya no usa horario de verano (UTC-6 todo el año).
    // La BD entrega las horas en UTC; sin esta conversión el alumno vería la recogida seis horas corrida.
    private static readonly TimeSpan CentroDeMexico = TimeSpan.FromHours(-6);

    public static (string Asunto, string Html, string Texto) Crear(string nombre, AvisoPedido a, string? enlace)
    {
        var (asunto, titulo, mensaje) = Contenido(a);
        var resumen = ResumenTexto(a);
        var texto = new StringBuilder()
            .Append($"Hola, {nombre}.\n\n{mensaje}\n\n");
        if (a.CodigoRecogida is not null)
        {
            texto.Append($"Tu código de recogida: {a.CodigoRecogida}\n\n");
        }
        texto.Append(resumen);
        if (enlace is not null)
        {
            texto.Append($"\nSigue tu pedido aquí: {enlace}\n");
        }
        texto.Append("\nSi no hiciste este pedido, ignora este mensaje.");

        return (LimpiarAsunto(asunto), Html(nombre, a, titulo, mensaje, enlace), texto.ToString());
    }

    private static (string Asunto, string Titulo, string Mensaje) Contenido(AvisoPedido a) => a.Evento switch
    {
        EventoPedido.Creada when a.EstadoPago == EstadoPago.Pendiente => (
            $"Recibimos tu pedido en {a.Cafeteria}",
            "Recibimos tu pedido",
            $"{a.Cafeteria} está esperando confirmar tu pago por {Metodo(a.Metodo)}. Te avisaremos en cuanto lo confirme."),
        EventoPedido.Creada => (
            $"Recibimos tu pedido en {a.Cafeteria}",
            "Recibimos tu pedido",
            $"Tu pedido quedó confirmado en {a.Cafeteria}. Te avisaremos cuando empiece a prepararse."),
        EventoPedido.PagoConfirmado => (
            "Confirmamos tu pago",
            "Confirmamos tu pago",
            $"{a.Cafeteria} confirmó tu pago. Te avisaremos cuando empiece a prepararse."),
        EventoPedido.EnPreparacion => (
            "Tu pedido se está preparando",
            "Tu pedido se está preparando",
            $"{a.Cafeteria} ya está preparando tu pedido. Te avisaremos cuando esté listo."),
        EventoPedido.Lista => (
            "¡Tu pedido está listo!",
            "¡Tu pedido está listo!",
            $"Pasa a recogerlo en {a.PuntoEntrega}. Muestra tu código de recogida en {a.Cafeteria} para que te lo entreguen."),
        EventoPedido.Entregada => (
            "Entregamos tu pedido",
            "Entregamos tu pedido",
            "Ya recibiste tu pedido. ¡Buen provecho!"),
        EventoPedido.Cancelada => (
            "Tu pedido fue cancelado",
            "Tu pedido fue cancelado",
            string.IsNullOrWhiteSpace(a.MotivoCancelacion)
                ? $"Tu pedido en {a.Cafeteria} fue cancelado."
                : $"Tu pedido en {a.Cafeteria} fue cancelado. Motivo: {a.MotivoCancelacion}"),
        _ => throw new ArgumentOutOfRangeException(nameof(a), a.Evento, "Evento de pedido desconocido")
    };

    private static string ResumenTexto(AvisoPedido a)
    {
        var sb = new StringBuilder($"Pedido en {a.Cafeteria}\nRecogida: {Hora(a.RecogidaEn)} en {a.PuntoEntrega}\n");
        foreach (var l in a.Lineas)
        {
            sb.Append($"  {l.Cantidad} x {l.Producto} - {Dinero(l.PrecioUnitario * l.Cantidad)}\n");
        }
        return sb.Append($"Total: {Dinero(a.Total)}\n").ToString();
    }

    private static string Html(string nombre, AvisoPedido a, string titulo, string mensaje, string? enlace)
    {
        static string h(string texto) => WebUtility.HtmlEncode(texto);
        var lineas = string.Concat(a.Lineas.Select(l =>
            $"<tr><td style=\"padding:2px 0\">{l.Cantidad} × {h(l.Producto)}</td>" +
            $"<td style=\"text-align:right\">{Dinero(l.PrecioUnitario * l.Cantidad)}</td></tr>"));
        var codigo = a.CodigoRecogida is null
            ? string.Empty
            : $"""
              <p style="text-align:center;margin:24px 0">Tu código de recogida<br>
                <strong style="font-size:32px;letter-spacing:6px;color:#c2410c">{h(a.CodigoRecogida)}</strong></p>
              """;
        var boton = enlace is null
            ? string.Empty
            : $"""
              <p style="text-align:center;margin:24px 0">
                <a href="{h(enlace)}" style="background:#c2410c;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none">
                  Seguir mi pedido
                </a>
              </p>
              """;

        return $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;color:#222">
              <h2 style="color:#c2410c">{h(titulo)}</h2>
              <p>Hola, {h(nombre)}.</p>
              <p>{h(mensaje)}</p>
              {codigo}
              <table style="width:100%;border-collapse:collapse;font-size:14px">
                <tr><td colspan="2" style="padding-bottom:6px"><strong>{h(a.Cafeteria)}</strong><br>
                  Recogida: {h(Hora(a.RecogidaEn))} en {h(a.PuntoEntrega)}</td></tr>
                {lineas}
                <tr><td style="padding-top:6px"><strong>Total</strong></td>
                    <td style="text-align:right;padding-top:6px"><strong>{Dinero(a.Total)}</strong></td></tr>
              </table>
              {boton}
              <p style="font-size:13px;color:#555">Si no hiciste este pedido, ignora este mensaje.</p>
            </div>
            """;
    }

    private static string Dinero(decimal monto) => "$" + monto.ToString("F2", CultureInfo.InvariantCulture);

    private static string Hora(DateTimeOffset instante) =>
        instante.ToOffset(CentroDeMexico).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static string Metodo(MetodoPago metodo) => metodo switch
    {
        MetodoPago.Efectivo => "efectivo",
        MetodoPago.Tarjeta => "tarjeta",
        MetodoPago.Transferencia => "transferencia",
        _ => "el método elegido"
    };

    // Quita saltos de línea y otros caracteres de control del asunto
    internal static string LimpiarAsunto(string asunto) =>
        string.Concat(asunto.Select(c => char.IsControl(c) ? ' ' : c)).Trim();
}
