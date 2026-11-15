using CampusBite.Api.Tests.Seguridad;
using Microsoft.AspNetCore.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CampusBite.Api.Tests.BdReal;

// JJ-Sprint5 13/11/2026: PostgreSQL real para probar API + repositorios + procedimientos de punta a punta.
//  - Servidor: CAMPUSBITE_PRUEBAS_PG (cadena a un PostgreSQL con permiso de CREATE DATABASE) o, si no está, un
//    contenedor postgres:17 con Testcontainers (en el CI de GitHub hay Docker).
//  - Crea una base temporal cb_pruebas_*, le carga el consolidado local más reciente (con datos de 90_datos_dev) y la
//    borra al terminar: nunca toca la base de trabajo.
//  - Sin Docker ni variable, las pruebas se marcan como omitidas (Skip), no como fallidas.
public sealed class BdDePrueba : IAsyncLifetime
{
    public const string VariableServidor = "CAMPUSBITE_PRUEBAS_PG";

    private PostgreSqlContainer? _contenedor;
    private string? _servidor;
    private string? _nombreBd;

    public string? Conexion { get; private set; }
    public string MotivoSinBd { get; private set; } = string.Empty;
    public ApiConBd? Api { get; private set; }

    public async Task InitializeAsync()
    {
        _servidor = Environment.GetEnvironmentVariable(VariableServidor);
        if (string.IsNullOrWhiteSpace(_servidor))
        {
            try
            {
                _contenedor = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
                await _contenedor.StartAsync();
                _servidor = _contenedor.GetConnectionString();
            }
            catch (Exception ex)
            {
                MotivoSinBd = $"Sin PostgreSQL para pruebas (ni Docker ni {VariableServidor}): {ex.Message}";
                return;
            }
        }

        _nombreBd = "cb_pruebas_" + Guid.NewGuid().ToString("N")[..10];
        await using (var servidor = new NpgsqlConnection(_servidor))
        {
            await servidor.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {_nombreBd}", servidor).ExecuteNonQueryAsync();
        }
        Conexion = new NpgsqlConnectionStringBuilder(_servidor) { Database = _nombreBd }.ConnectionString;

        // El consolidado trae comandos de psql (\set, \echo) que Npgsql no entiende: se quitan
        var sql = string.Join('\n', File.ReadLines(ConsolidadoMasReciente()).Where(l => !l.StartsWith('\\')));
        await using (var bd = new NpgsqlConnection(Conexion))
        {
            await bd.OpenAsync();
            await new NpgsqlCommand(sql, bd) { CommandTimeout = 300 }.ExecuteNonQueryAsync();
        }

        Api = new ApiConBd(Conexion);
    }

    public async Task DisposeAsync()
    {
        if (Api is not null) await Api.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        if (_nombreBd is not null && _contenedor is null)
        {
            await using var servidor = new NpgsqlConnection(_servidor);
            await servidor.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS {_nombreBd} WITH (FORCE)", servidor).ExecuteNonQueryAsync();
        }
        if (_contenedor is not null) await _contenedor.DisposeAsync();
    }

    // Ejecuta SQL directo (preparar datos que la API no expone, p. ej. una contraseña conocida)
    public async Task EjecutarAsync(string sql, params NpgsqlParameter[] parametros)
    {
        await using var bd = new NpgsqlConnection(Conexion);
        await bd.OpenAsync();
        await using var comando = new NpgsqlCommand(sql, bd);
        comando.Parameters.AddRange(parametros);
        await comando.ExecuteNonQueryAsync();
    }

    // db/consolidado/versiones/campusbite_V*.sql (local, no _prod) con el número de versión más alto
    private static string ConsolidadoMasReciente()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "db", "consolidado", "versiones")))
        {
            dir = dir.Parent;
        }
        if (dir is null) throw new DirectoryNotFoundException("No se encontró db/consolidado/versiones");

        return Directory.GetFiles(Path.Combine(dir.FullName, "db", "consolidado", "versiones"), "campusbite_V*.sql")
            .Where(f => !f.EndsWith("_prod.sql", StringComparison.Ordinal))
            .OrderBy(f => Version.Parse(Path.GetFileNameWithoutExtension(f)["campusbite_V".Length..]))
            .Last();
    }
}

// La API de prueba (token real, endpoints de prueba) apuntando a la base temporal
public sealed class ApiConBd(string conexion) : ApiDePrueba
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Database", conexion);
    }
}

// JJ-Sprint5 13/11/2026: sin paralelismo con las demás colecciones. En CI comparte el servidor con
// Postgres/PostgresDePrueba y las dos cargan el consolidado (ALTER ROLE app_backend, GRANT): a la vez pueden chocar.
[CollectionDefinition(Nombre, DisableParallelization = true)]
public sealed class ColeccionBdReal : ICollectionFixture<BdDePrueba>
{
    public const string Nombre = "BD real";
}
