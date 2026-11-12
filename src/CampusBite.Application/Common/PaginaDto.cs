namespace CampusBite.Application.Common;

// JY-Sprint4 31/10/2026: una página de resultados. Total es el de todos los que cumplen el filtro, no solo los de
// esta página, para que el cliente sepa cuántas páginas hay.
public class PaginaDto<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Total { get; init; }
    public required int Pagina { get; init; }
    public required int TamanoPagina { get; init; }
}
