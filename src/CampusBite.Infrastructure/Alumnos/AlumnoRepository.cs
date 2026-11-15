using CampusBite.Application.Alumnos;
using CampusBite.Domain.Cafeterias;
using CampusBite.Infrastructure.Persistence;
using static CampusBite.Infrastructure.Persistence.Procedimientos;

namespace CampusBite.Infrastructure.Alumnos;

// JJ-Sprint5 13/11/2026: implementa IAlumnoRepository con los sp_* de perfil (035) y favoritos (036)
public class AlumnoRepository : IAlumnoRepository
{
    private readonly AppDbContext _context;

    public AlumnoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PerfilAlumnoDto> ObtenerPerfilAsync(Guid idAlumno, CancellationToken cancellationToken = default) =>
        (await _context.EjecutarAsync(
            "CALL sp_obtener_perfil_alumno(@alumno, NULL)",
            [Id("alumno", idAlumno)],
            fila => fila.LeerJson<PerfilAlumnoDto>(0),
            cancellationToken))!;

    public Task ActualizarDatosAsync(Guid idAlumno, DatosAlumnoDto datos, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_actualizar_perfil_alumno(@alumno, @nombre, @matricula, @telefono)",
            [Id("alumno", idAlumno), Texto("nombre", datos.Nombre), Texto("matricula", datos.Matricula), Texto("telefono", datos.Telefono)],
            cancellationToken);

    public Task GuardarPreferenciasAsync(Guid idAlumno, PreferenciasAlumnoDto p, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_guardar_preferencias_alumno(@alumno, @cafeteria, @metodo::metodo_pago, @listo, @promos)",
            [Id("alumno", idAlumno), IdOpcional("cafeteria", p.IdCafeteriaFrecuente), Enumeracion<MetodoPago>("metodo", p.MetodoPago),
             Logico("listo", p.AvisoPedidoListo), Logico("promos", p.AvisoPromociones)],
            cancellationToken);

    public Task<IReadOnlyList<FavoritoCafeteriaDto>> FavoritosCafeteriasAsync(Guid idAlumno, CancellationToken cancellationToken = default) =>
        Lista<FavoritoCafeteriaDto>("CALL sp_favoritos_cafeterias(@alumno, NULL)", idAlumno, cancellationToken);

    public async Task<FavoritoCafeteriaDto> GuardarFavoritoCafeteriaAsync(Guid idAlumno, Guid idCafeteria, CancellationToken cancellationToken = default) =>
        (await _context.EjecutarAsync(
            "CALL sp_guardar_favorito_cafeteria(@alumno, @id, NULL)",
            [Id("alumno", idAlumno), Id("id", idCafeteria)],
            fila => fila.LeerJson<FavoritoCafeteriaDto>(0),
            cancellationToken))!;

    public Task QuitarFavoritoCafeteriaAsync(Guid idAlumno, Guid idCafeteria, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_quitar_favorito_cafeteria(@alumno, @id)",
            [Id("alumno", idAlumno), Id("id", idCafeteria)],
            cancellationToken);

    public Task<IReadOnlyList<FavoritoPlatilloDto>> FavoritosPlatillosAsync(Guid idAlumno, CancellationToken cancellationToken = default) =>
        Lista<FavoritoPlatilloDto>("CALL sp_favoritos_productos(@alumno, NULL)", idAlumno, cancellationToken);

    public async Task<FavoritoPlatilloDto> GuardarFavoritoPlatilloAsync(Guid idAlumno, Guid idProducto, CancellationToken cancellationToken = default) =>
        (await _context.EjecutarAsync(
            "CALL sp_guardar_favorito_producto(@alumno, @id, NULL)",
            [Id("alumno", idAlumno), Id("id", idProducto)],
            fila => fila.LeerJson<FavoritoPlatilloDto>(0),
            cancellationToken))!;

    public Task QuitarFavoritoPlatilloAsync(Guid idAlumno, Guid idProducto, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_quitar_favorito_producto(@alumno, @id)",
            [Id("alumno", idAlumno), Id("id", idProducto)],
            cancellationToken);

    private Task<IReadOnlyList<T>> Lista<T>(string sql, Guid idAlumno, CancellationToken cancellationToken) =>
        _context.EjecutarAsync<IReadOnlyList<T>>(
            sql, [Id("alumno", idAlumno)], fila => fila.LeerJson<List<T>>(0) ?? [], cancellationToken);
}
