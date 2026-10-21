using CampusBite.Application.Cafeterias;
using CampusBite.Application.Cafeterias.Dtos;
using CampusBite.Infrastructure.Persistence;
using static CampusBite.Infrastructure.Persistence.Procedimientos;

namespace CampusBite.Infrastructure.Cafeterias;

// JY-Sprint3 20/10/2026: TEC-08. Implementa ICafeteriaRepository con los procedimientos sp_*_cafeteria(s).
// Los permisos (admin_sistema / encargado de esa cafetería) los valida la BD con el idActor.
// Los errores de la BD llegan ya traducidos a excepciones de Domain (Persistence/Procedimientos.cs).
public class CafeteriaRepository : ICafeteriaRepository
{
    private readonly AppDbContext _context;

    public CafeteriaRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<(IReadOnlyList<CafeteriaDto> Cafeterias, int Total)> ListarAsync(
        Guid idActor, bool incluirInactivas, string? buscar, int limite, int desplazamiento,
        CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync<(IReadOnlyList<CafeteriaDto>, int)>(
            "CALL sp_listar_cafeterias(@actor, @inactivas, @buscar, @limite, @desplazamiento, NULL, NULL)",
            [Id("actor", idActor), Logico("inactivas", incluirInactivas), Texto("buscar", buscar),
             Entero("limite", limite), Entero("desplazamiento", desplazamiento)],
            fila => (fila.LeerJson<List<CafeteriaDto>>(1) ?? [], fila.GetInt32(0)),
            cancellationToken);

    public Task<CafeteriaDto?> ObtenerAsync(Guid idCafeteria, Guid idActor, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_obtener_cafeteria(@id, @actor, NULL)",
            [Id("id", idCafeteria), Id("actor", idActor)],
            fila => fila.LeerJson<CafeteriaDto>(0),
            cancellationToken);

    public Task<Guid> CrearAsync(Guid idActor, DatosCafeteria datos, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_crear_cafeteria(@actor, @nombre, @ubicacion, @campus, @punto, @metodos::metodo_pago[], NULL)",
            [Id("actor", idActor), .. Datos(datos)],
            fila => fila.GetGuid(0),
            cancellationToken);

    public Task ActualizarAsync(Guid idCafeteria, Guid idActor, DatosCafeteria datos, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_actualizar_cafeteria(@id, @actor, @nombre, @ubicacion, @campus, @punto, @metodos::metodo_pago[])",
            [Id("id", idCafeteria), Id("actor", idActor), .. Datos(datos)],
            cancellationToken);

    public Task CambiarActivaAsync(Guid idCafeteria, bool activa, Guid idActor, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_cambiar_activa_cafeteria(@id, @activa, @actor)",
            [Id("id", idCafeteria), Logico("activa", activa), Id("actor", idActor)],
            cancellationToken);

    private static Npgsql.NpgsqlParameter[] Datos(DatosCafeteria datos) =>
    [
        Texto("nombre", datos.Nombre),
        Texto("ubicacion", datos.Ubicacion),
        Logico("campus", datos.EsDelCampus),
        Texto("punto", datos.PuntoEntrega),
        Enumeraciones("metodos", datos.MetodosPago)
    ];
}
