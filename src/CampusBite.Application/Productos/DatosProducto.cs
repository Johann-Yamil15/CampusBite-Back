namespace CampusBite.Application.Productos;

// JY-Sprint4 27/10/2026: TEC-09. Datos ya normalizados (sin espacios extra, descripción vacía = null) que el servicio
// le pasa al repositorio. Separado del DTO para que el repositorio nunca reciba datos sin limpiar.
public record DatosProducto(
    string Nombre,
    string? Descripcion,
    decimal Precio,
    Guid? IdCategoria,
    bool Disponible);
