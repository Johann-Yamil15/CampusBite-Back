namespace CampusBite.Application.Productos.Dtos;

// JY-Sprint4 27/10/2026: TEC-09. Un producto como lo devuelve la API
public class ProductoDto
{
    public Guid IdProducto { get; init; }
    public Guid IdCafeteria { get; init; }
    public Guid? IdCategoria { get; init; }
    public string? Categoria { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public decimal Precio { get; init; }
    public string? ImagenUrl { get; init; }
    public bool Disponible { get; init; }
    public DateTimeOffset CreadoEn { get; init; }
    public DateTimeOffset ActualizadoEn { get; init; }
}
