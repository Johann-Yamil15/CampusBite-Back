namespace CampusBite.Application.Common.Dtos;

// JY-Sprint3 20/10/2026: una página de resultados. Sirve para cualquier listado (cafeterías, productos, órdenes...).
public class PaginaDto<T>
{
    public IReadOnlyList<T> Elementos { get; init; } = [];
    public int Total { get; init; }          // cuántos hay en total con ese filtro
    public int Pagina { get; init; }         // empieza en 1
    public int Tamano { get; init; }
    public int TotalPaginas => Tamano > 0 ? (Total + Tamano - 1) / Tamano : 0;
}
