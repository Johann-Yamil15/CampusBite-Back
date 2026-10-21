using System.ComponentModel.DataAnnotations;

namespace CampusBite.Application.Cafeterias.Dtos;

// JY-Sprint3 20/10/2026: TEC-08. Filtro y página de GET /api/cafeterias (?buscar=&pagina=&tamano=&incluirInactivas=).
// Paginado por las pruebas de estrés: con 10 mil cafeterías, la lista completa eran 3 MB y ~150 ms por llamada.
public class ListarCafeteriasDto
{
    // Texto dentro del nombre, sin distinguir mayúsculas
    [MaxLength(100, ErrorMessage = "La búsqueda puede tener máximo 100 caracteres.")]
    public string? Buscar { get; set; }

    [Range(1, 100_000, ErrorMessage = "La página debe ser 1 o mayor.")]
    public int Pagina { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "El tamaño de página debe estar entre 1 y 100.")]
    public int Tamano { get; set; } = 20;

    // Solo admin del sistema
    public bool IncluirInactivas { get; set; }
}
