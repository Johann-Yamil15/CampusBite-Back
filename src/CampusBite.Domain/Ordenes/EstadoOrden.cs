namespace CampusBite.Domain.Ordenes;

// JJ-Sprint5 13/11/2026: ENUM estado_orden de la BD. En la API viaja como texto ("EnPreparacion");
// Infrastructure lo convierte a snake_case ("en_preparacion").
public enum EstadoOrden
{
    Pendiente = 1,
    Confirmada = 2,
    EnPreparacion = 3,
    Lista = 4,
    Entregada = 5,
    Cancelada = 6
}
