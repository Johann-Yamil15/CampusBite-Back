using System.Globalization;
using System.Text;
using CampusBite.Application.Common;
using CampusBite.Application.Menu.Dtos;
using CampusBite.Domain.Productos;

namespace CampusBite.Application.Menu;

// JJ-Sprint5 13/11/2026: GET /api/menu/buscar (P2). Misma semántica que el filtro del front, paginado.
// shortcut: filtra en memoria sobre el menú completo (sp_menu_cafeteria); si el catálogo crece mucho, mover el
// filtro a un procedimiento con índices de texto.
public enum OrdenBusqueda
{
    Relevancia = 1,
    PrecioAsc = 2,
    PrecioDesc = 3,
    Calificacion = 4,
    Tiempo = 5
}

public class BuscarMenuQuery
{
    public string? Texto { get; init; }
    public string? Categoria { get; init; }
    public decimal? PrecioMax { get; init; }
    public List<Dieta> Dietas { get; init; } = [];
    public bool SoloAbiertas { get; init; }
    public OrdenBusqueda Orden { get; init; } = OrdenBusqueda.Relevancia;
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = 20;
}

public class PlatilloCatalogoDto : ProductoMenuDto
{
    public Guid IdCafeteria { get; init; }
    public required string Cafeteria { get; init; }
    public bool CafeteriaAbierta { get; init; }
}

public static class BuscadorMenu
{
    public static PaginaDto<PlatilloCatalogoDto> Buscar(IEnumerable<MenuCafeteriaDto> menu, BuscarMenuQuery q)
    {
        var texto = Normalizar(q.Texto);
        var pagina = Math.Max(q.Pagina, 1);
        var tamano = Math.Clamp(q.TamanoPagina, 1, 100);

        var platillos = menu
            .Where(c => !q.SoloAbiertas || c.Abierta)
            .SelectMany(c => c.Productos.Select(p => new PlatilloCatalogoDto
            {
                IdProducto = p.IdProducto, Producto = p.Producto, Categoria = p.Categoria, Descripcion = p.Descripcion,
                Precio = p.Precio, ImagenUrl = p.ImagenUrl, Calificacion = p.Calificacion,
                MinutosPreparacion = p.MinutosPreparacion, PrecioAnterior = p.PrecioAnterior, Popular = p.Popular,
                Dietas = p.Dietas, IdCafeteria = c.IdCafeteria, Cafeteria = c.Cafeteria, CafeteriaAbierta = c.Abierta
            }))
            .Where(p => texto.Length == 0 ||
                        Normalizar($"{p.Producto} {p.Descripcion} {p.Categoria} {p.Cafeteria}").Contains(texto))
            .Where(p => string.IsNullOrWhiteSpace(q.Categoria) || Normalizar(p.Categoria) == Normalizar(q.Categoria))
            .Where(p => q.PrecioMax is null || p.Precio <= q.PrecioMax)
            .Where(p => q.Dietas.All(d => p.Dietas.Contains(d)));

        platillos = q.Orden switch
        {
            OrdenBusqueda.PrecioAsc => platillos.OrderBy(p => p.Precio),
            OrdenBusqueda.PrecioDesc => platillos.OrderByDescending(p => p.Precio),
            OrdenBusqueda.Calificacion => platillos.OrderByDescending(p => p.Calificacion ?? 0),
            OrdenBusqueda.Tiempo => platillos.OrderBy(p => p.MinutosPreparacion ?? int.MaxValue),
            // Relevancia: primero lo que coincide en el nombre, luego los populares
            _ => platillos.OrderByDescending(p => texto.Length > 0 && Normalizar(p.Producto).Contains(texto))
                          .ThenByDescending(p => p.Popular)
        };

        var lista = platillos.ToList();
        return new PaginaDto<PlatilloCatalogoDto>
        {
            Items = lista.Skip((pagina - 1) * tamano).Take(tamano).ToList(),
            Total = lista.Count,
            Pagina = pagina,
            TamanoPagina = tamano
        };
    }

    // Sin acentos ni mayúsculas: "Café" = "cafe"
    private static string Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        var sb = new StringBuilder();
        foreach (var c in valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString();
    }
}
