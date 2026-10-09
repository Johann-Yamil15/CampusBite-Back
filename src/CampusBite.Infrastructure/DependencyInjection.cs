using CampusBite.Application.Auth;
using CampusBite.Application.Common.Security;
using CampusBite.Infrastructure.Auth;
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

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());
    }
}
