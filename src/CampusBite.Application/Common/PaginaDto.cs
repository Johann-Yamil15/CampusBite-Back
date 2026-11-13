namespace CampusBite.Application.Common;

// JY-Sprint4 31/10/2026: una página de resultados. Total es el de todos los que cumplen el filtro, no solo los de
// esta página. JY-Sprint4 05/11/2026: TotalPaginas, TieneAnterior y TieneSiguiente para que el cliente (scroll
// infinito o botones de página) no tenga que calcularlos.
public class PaginaDto<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Total { get; init; }
    public required int Pagina { get; init; }
    public required int TamanoPagina { get; init; }

    public int TotalPaginas => TamanoPagina > 0 ? (Total + TamanoPagina - 1) / TamanoPagina : 0;
    public bool TieneAnterior => Pagina > 1;
    public bool TieneSiguiente => Pagina < TotalPaginas;
}
