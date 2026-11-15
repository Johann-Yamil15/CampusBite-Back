using System.ComponentModel.DataAnnotations;
using CampusBite.Domain.Cafeterias;

namespace CampusBite.Application.Alumnos;

// JJ-Sprint5 13/11/2026: perfil, preferencias y favoritos del alumno (forma de los sp_* de 035/036)
public class PerfilAlumnoDto
{
    public required string Correo { get; init; }
    public required DatosAlumnoDto Datos { get; init; }
    public required PreferenciasAlumnoDto Preferencias { get; init; }
}

// Request y response de PUT api/alumnos/me/perfil: mismas reglas que el registro
public class DatosAlumnoDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(2, ErrorMessage = "El nombre debe tener al menos 2 caracteres.")]
    [MaxLength(80, ErrorMessage = "El nombre puede tener máximo 80 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "La matrícula puede tener máximo 20 caracteres.")]
    public string? Matricula { get; set; }

    [RegularExpression(@"^[0-9+]{10,15}$", ErrorMessage = "El teléfono debe tener de 10 a 15 dígitos.")]
    public string? Telefono { get; set; }
}

// No se guardan datos de tarjetas, solo el método preferido
public class PreferenciasAlumnoDto
{
    public Guid? IdCafeteriaFrecuente { get; set; }
    public MetodoPago? MetodoPago { get; set; }
    public bool AvisoPedidoListo { get; set; } = true;
    public bool AvisoPromociones { get; set; }
}

public class FavoritoCafeteriaDto
{
    public Guid IdCafeteria { get; init; }
    public required string Nombre { get; init; }
    public string? Foto { get; init; }
    public decimal? Calificacion { get; init; }
    public string PuntoEntrega { get; init; } = string.Empty;
    public bool EsDelCampus { get; init; }
    public bool Abierta { get; init; }
    public int Platillos { get; init; }
}

public class FavoritoPlatilloDto
{
    public Guid IdProducto { get; init; }
    public required string Producto { get; init; }
    public decimal Precio { get; init; }
    public string? ImagenUrl { get; init; }
    public Guid IdCafeteria { get; init; }
    public required string Cafeteria { get; init; }
    public bool Disponible { get; init; }
    public bool CafeteriaAbierta { get; init; }
}
