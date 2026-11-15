using CampusBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampusBite.Api.Tests.Postgres;

// Pruebas de Infrastructure contra un PostgreSQL REAL y desechable (nunca Neon ni Azure).
//  - Servidor: variable CAMPUSBITE_PRUEBAS_PG con una cadena a un PostgreSQL local con permiso de CREATE DATABASE
//    (p. ej. un contenedor postgres:16). Sin la variable, estas pruebas se marcan como OMITIDAS, no como aprobadas.
//  - Crea una base temporal cb_infra_*, le carga el consolidado local más reciente de db/consolidado/versiones (con los
//    datos de 90_datos_dev) y la borra al terminar.
//  - Solo usa Npgsql y EF Core, que ya son dependencias de Infrastructure: no agrega paquetes.
public sealed class PostgresDePrueba : IAsyncLifetime
{
    public const string Variable = "CAMPUSBITE_PRUEBAS_PG";
    public static readonly bool Disponible = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable));

    // Datos de 90_datos_dev/002_datos_prueba.sql
    public static readonly Guid Alumno = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid Encargado = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    public static readonly Guid Admin = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    public static readonly Guid Cafeteria = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    public static readonly Guid Desayunos = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
    public static readonly Guid Bebidas = Guid.Parse("00000000-0000-0000-0000-0000000000d2");
    public static readonly Guid Chilaquiles = Guid.Parse("00000000-0000-0000-0000-0000000000e1");
    public static readonly Guid Sandwich = Guid.Parse("00000000-0000-0000-0000-0000000000e2");
    public static readonly Guid Cafe = Guid.Parse("00000000-0000-0000-0000-0000000000e3");

    private string? _servidor;
    private string? _nombreBd;

    public string Conexion { get; private set; } = string.Empty;
    public string Consolidado { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!Disponible)
        {
            return;
        }
        _servidor = Environment.GetEnvironmentVariable(Variable)!;
        _nombreBd = "cb_infra_" + Guid.NewGuid().ToString("N")[..10];
        await using (var servidor = new NpgsqlConnection(_servidor))
        {
            await servidor.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {_nombreBd}", servidor).ExecuteNonQueryAsync();
        }
        Conexion = new NpgsqlConnectionStringBuilder(_servidor) { Database = _nombreBd }.ConnectionString;

        Consolidado = ConsolidadoMasReciente();
        // El consolidado trae comandos de psql (\set, \echo) que Npgsql no entiende: se quitan
        var sql = string.Join('\n', File.ReadLines(Consolidado).Where(l => !l.StartsWith('\\')));
        await using var bd = new NpgsqlConnection(Conexion);
        await bd.OpenAsync();
        await new NpgsqlCommand(sql, bd) { CommandTimeout = 300 }.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (_nombreBd is null)
        {
            return;
        }
        NpgsqlConnection.ClearAllPools();
        await using var servidor = new NpgsqlConnection(_servidor);
        await servidor.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE IF EXISTS {_nombreBd} WITH (FORCE)", servidor).ExecuteNonQueryAsync();
    }

    // Un DbContext nuevo por uso, como en la API (scoped)
    public AppDbContext Contexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conexion).Options);

    public async Task EjecutarAsync(string sql, params NpgsqlParameter[] parametros)
    {
        await using var bd = new NpgsqlConnection(Conexion);
        await bd.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, bd);
        comando.Parameters.AddRange(parametros);
        await comando.ExecuteNonQueryAsync();
    }

    public async Task<T> EscalarAsync<T>(string sql, params NpgsqlParameter[] parametros)
    {
        await using var bd = new NpgsqlConnection(Conexion);
        await bd.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, bd);
        comando.Parameters.AddRange(parametros);
        return (T)(await comando.ExecuteScalarAsync())!;
    }

    // db/consolidado/versiones/campusbite_V*.sql (no _prod) con el número de versión más alto
    private static string ConsolidadoMasReciente()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "db", "consolidado", "versiones")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
        {
            throw new DirectoryNotFoundException("No se encontró db/consolidado/versiones");
        }
        return Directory.GetFiles(Path.Combine(dir.FullName, "db", "consolidado", "versiones"), "campusbite_V*.sql")
            .Where(f => !f.EndsWith("_prod.sql", StringComparison.Ordinal))
            .OrderBy(f => Version.Parse(Path.GetFileNameWithoutExtension(f)["campusbite_V".Length..]))
            .Last();
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionPostgres : ICollectionFixture<PostgresDePrueba>
{
    public const string Nombre = "PostgreSQL real";
}

// [Fact]/[Theory] que se OMITEN (no aprueban) si no hay servidor de pruebas
public sealed class FactPostgresAttribute : FactAttribute
{
    public FactPostgresAttribute()
    {
        if (!PostgresDePrueba.Disponible)
        {
            Skip = $"Sin PostgreSQL de pruebas: define {PostgresDePrueba.Variable}";
        }
    }
}

public sealed class TheoryPostgresAttribute : TheoryAttribute
{
    public TheoryPostgresAttribute()
    {
        if (!PostgresDePrueba.Disponible)
        {
            Skip = $"Sin PostgreSQL de pruebas: define {PostgresDePrueba.Variable}";
        }
    }
}
