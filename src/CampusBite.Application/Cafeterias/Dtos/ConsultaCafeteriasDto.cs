using System.ComponentModel.DataAnnotations;

namespace CampusBite.Application.Cafeterias.Dtos;

// JY-Sprint4 31/10/2026: parámetros de GET /api/cafeterias. Se pagina para que la PWA no descargue miles de
// cafeterías de golpe; los límites son los mismos que aplica sp_listar_cafeterias.
public class ConsultaCafeteriasDto
{
    public const int TamanoMaximo = 100;

    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int Pagina { get; set; } = 1;

    [Range(1, TamanoMaximo, ErrorMessage = "El tamaño de página va de 1 a 100.")]
    public int TamanoPagina { get; set; } = 20;

    // Parte del nombre, sin distinguir mayúsculas
    [MaxLength(100, ErrorMessage = "La búsqueda puede tener máximo 100 caracteres.")]
    public string? Buscar { get; set; }

    // Solo el admin del sistema
    public bool IncluirInactivas { get; set; }
}
