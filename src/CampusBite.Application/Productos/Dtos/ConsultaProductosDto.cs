using CampusBite.Application.Common;

namespace CampusBite.Application.Productos.Dtos;

// JY-Sprint4 05/11/2026: parámetros de GET /api/cafeterias/{id}/productos (panel del encargado). Paginado porque una
// cafetería puede tener miles de productos; con paginación el filtro por categoría ya no puede hacerse en el navegador.
public class ConsultaProductosDto : ConsultaPaginadaDto
{
    // null = todas las categorías
    public Guid? IdCategoria { get; set; }
}
