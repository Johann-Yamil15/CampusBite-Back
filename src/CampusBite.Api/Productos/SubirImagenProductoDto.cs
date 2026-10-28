using System.ComponentModel.DataAnnotations;

namespace CampusBite.Api.Productos;

// JY-Sprint4 27/10/2026: TEC-10. Formulario multipart/form-data de la subida (campo "archivo").
// Vive en la Api y no en Application porque IFormFile es de ASP.NET; el controlador lo convierte en ArchivoImagen.
// Aquí solo se exige que venga; tipo, tamaño y contenido los valida ReglasImagenProducto.
public class SubirImagenProductoDto
{
    [Required(ErrorMessage = "Adjunta la imagen en el campo \"archivo\".")]
    public IFormFile? Archivo { get; set; }
}
