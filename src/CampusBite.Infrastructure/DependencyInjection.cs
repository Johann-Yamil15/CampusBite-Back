using CampusBite.Application.Auth;
using CampusBite.Application.Cafeterias;
using CampusBite.Application.Common.Security;
using CampusBite.Infrastructure.Auth;
using CampusBite.Infrastructure.Cafeterias;
using CampusBite.Infrastructure.Persistence;
using CampusBite.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CampusBite.Infrastructure;

// JJ-Sprint2 09/10/2026: registra las implementaciones técnicas (BD, repositorios, BCrypt, JWT); agregar aquí cada repositorio nuevo
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddDatabase(services, configuration);

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();

        // Repositorios por módulo
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<ICafeteriaRepository, CafeteriaRepository>();

        return services;
    }

    // JJ-Sprint2 09/10/2026: elige la conexión. Database__Conexion=Local|Produccion usa ConnectionStrings__Local / __Produccion
    // (desarrollo); si no está definida usa ConnectionStrings__Database (así está configurado Azure)
    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var conexion = configuration["Database:Conexion"];
        var nombre = string.IsNullOrWhiteSpace(conexion) ? "Database" : conexion.Trim();

        var connectionString = configuration.GetConnectionString(nombre);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{nombre} no está configurada. Define ConnectionStrings__{nombre} en el archivo .env " +
                "o en las variables de entorno (ver README, sección \"Conexiones a la base de datos\").");
        }

        var datos = new NpgsqlConnectionStringBuilder(connectionString);
        services.AddSingleton(new DatabaseInfo(nombre, datos.Host ?? "?", datos.Database ?? "?", datos.Username ?? "?"));

        // JY-Sprint3 20/10/2026: la BD limita a app_backend a 50 conexiones (07_seguridad/002_limites_rol_api.sql) y
        // Npgsql abre hasta 100 por defecto. La prueba de carga (100 usuarios a la vez) dio 2 mil errores 500
        // "too many connections for role". Con el pool por debajo del límite, si se llena las peticiones esperan
        // turno en lugar de fallar. Si hay varias instancias de la API, la suma de sus pools debe quedar bajo 50.
        // Se respeta si la cadena ya trae "Maximum Pool Size"; si no, Database__MaxConexiones (40 por defecto).
        if (!connectionString.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.Contains("Max Pool Size", StringComparison.OrdinalIgnoreCase))
        {
            datos.MaxPoolSize = configuration.GetValue("Database:MaxConexiones", 40);
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(datos.ConnectionString)
                   .UseSnakeCaseNamingConvention());
    }
}
