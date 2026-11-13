namespace CampusBite.Application.Alumnos;

// JJ-Sprint5 13/11/2026: perfil, preferencias y favoritos del alumno de la sesión.
// shortcut: sin capa de servicio (como Menu): las reglas viven en los procedimientos.
public interface IAlumnoRepository
{
    Task<PerfilAlumnoDto> ObtenerPerfilAsync(Guid idAlumno, CancellationToken cancellationToken = default);

    // 409 si la matrícula ya es de otro
    Task ActualizarDatosAsync(Guid idAlumno, DatosAlumnoDto datos, CancellationToken cancellationToken = default);

    // 400 si la cafetería frecuente no existe o está inactiva
    Task GuardarPreferenciasAsync(Guid idAlumno, PreferenciasAlumnoDto preferencias, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoritoCafeteriaDto>> FavoritosCafeteriasAsync(Guid idAlumno, CancellationToken cancellationToken = default);
    Task<FavoritoCafeteriaDto> GuardarFavoritoCafeteriaAsync(Guid idAlumno, Guid idCafeteria, CancellationToken cancellationToken = default);
    Task QuitarFavoritoCafeteriaAsync(Guid idAlumno, Guid idCafeteria, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FavoritoPlatilloDto>> FavoritosPlatillosAsync(Guid idAlumno, CancellationToken cancellationToken = default);
    Task<FavoritoPlatilloDto> GuardarFavoritoPlatilloAsync(Guid idAlumno, Guid idProducto, CancellationToken cancellationToken = default);
    Task QuitarFavoritoPlatilloAsync(Guid idAlumno, Guid idProducto, CancellationToken cancellationToken = default);
}
