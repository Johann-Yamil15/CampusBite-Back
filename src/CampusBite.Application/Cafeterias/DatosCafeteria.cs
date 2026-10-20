using CampusBite.Domain.Cafeterias;

namespace CampusBite.Application.Cafeterias;

// JY-Sprint3 20/10/2026: TEC-08. Datos ya normalizados (sin espacios extra, métodos sin repetir) que el servicio
// le pasa al repositorio. Separado del DTO para que el repositorio nunca reciba datos sin limpiar.
public record DatosCafeteria(
    string Nombre,
    string Ubicacion,
    bool EsDelCampus,
    string PuntoEntrega,
    IReadOnlyList<MetodoPago> MetodosPago);
