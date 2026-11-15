using CampusBite.Domain.Productos;

namespace CampusBite.Application.Menu.Dtos;

// JY-Sprint4 05/11/2026: HU-07B. Un producto como lo ve el alumno en el menú: solo lo necesario para la
// tarjeta y el carrito. No trae "disponible" ni fechas: sp_menu_cafeteria ya filtra los no disponibles.
// JJ-Sprint5 13/11/2026: + calificacion (null), minutosPreparacion, precioAnterior (null), popular y dietas.
public class ProductoMenuDto
{
    public Guid IdProducto { get; init; }
    public required string Producto { get; init; }
    public string? Categoria { get; init; }
    public string? Descripcion { get; init; }
    public decimal Precio { get; init; }
    public string? ImagenUrl { get; init; }
    public decimal? Calificacion { get; init; }
    public int? MinutosPreparacion { get; init; }
    public decimal? PrecioAnterior { get; init; }
    public bool Popular { get; init; }
    public IReadOnlyList<Dieta> Dietas { get; init; } = [];
}
