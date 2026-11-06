using CampusBite.Application.Productos.Dtos;

namespace CampusBite.Api.Productos;

// JY-Sprint4 27/10/2026: TEC-09. Formulario multipart/form-data del alta: los datos del producto (con sus validaciones
// de GuardarProductoDto) y, opcional, la imagen en el campo "archivo". Vive en la Api porque IFormFile es de ASP.NET.
// La imagen la valida ReglasImagenProducto, igual que en POST .../imagen.
public class CrearProductoFormDto : GuardarProductoDto
{
    public IFormFile? Archivo { get; set; }
}
