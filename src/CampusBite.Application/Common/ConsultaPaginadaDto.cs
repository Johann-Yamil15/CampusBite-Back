using System.ComponentModel.DataAnnotations;

namespace CampusBite.Application.Common;

// JY-Sprint4 05/11/2026: parámetros comunes de los listados paginados (?pagina=&tamanoPagina=&buscar=). Los límites
// son los mismos que aplican los procedimientos (1 a 100 por página).
public class ConsultaPaginadaDto
{
    public const int TamanoMaximo = 100;

    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int Pagina { get; set; } = 1;

    [Range(1, TamanoMaximo, ErrorMessage = "El tamaño de página va de 1 a 100.")]
    public int TamanoPagina { get; set; } = 20;

    // Parte del nombre, sin distinguir mayúsculas
    [MaxLength(100, ErrorMessage = "La búsqueda puede tener máximo 100 caracteres.")]
    public string? Buscar { get; set; }

    // Búsqueda recortada; vacía = sin filtro
    public string? BuscarNormalizado() => string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim();

    public int Desplazamiento() => (Pagina - 1) * TamanoPagina;

    public PaginaDto<T> CrearPagina<T>(int total, IReadOnlyList<T> items) =>
        new() { Items = items, Total = total, Pagina = Pagina, TamanoPagina = TamanoPagina };
}
