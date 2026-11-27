namespace CampusBite.Application.Ordenes;

// JY-Sprint5 27/11/2026: el dueño de una orden, a quien se le avisa por correo (sp_destinatario_orden).
// CodigoRecogida es el de su propia orden: la cafetería nunca lo ve en la API, pero el correo es para el alumno.
public sealed record DestinatarioOrden(
    Guid IdUsuario, string Correo, string Nombre, bool AvisoPedidoListo, string CodigoRecogida);
