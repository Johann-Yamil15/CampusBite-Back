using CampusBite.Application.Common;

namespace CampusBite.Application.Cafeterias.Dtos;

// JY-Sprint4 31/10/2026: parámetros de GET /api/cafeterias. Se pagina para que la PWA no descargue miles de
// cafeterías de golpe (página, tamaño y búsqueda vienen de ConsultaPaginadaDto).
public class ConsultaCafeteriasDto : ConsultaPaginadaDto
{
    // Solo el admin del sistema
    public bool IncluirInactivas { get; set; }
}
