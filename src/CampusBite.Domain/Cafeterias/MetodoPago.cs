namespace CampusBite.Domain.Cafeterias;

// JY-Sprint3 20/10/2026: métodos de pago (ENUM metodo_pago de la BD: 'tarjeta', 'transferencia', 'efectivo').
// En la API viajan como texto ("Efectivo"); Infrastructure los convierte al valor en minúsculas de la BD.
public enum MetodoPago
{
    Tarjeta = 1,
    Transferencia = 2,
    Efectivo = 3
}
