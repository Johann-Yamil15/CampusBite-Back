using CampusBite.Application.Auth;
using CampusBite.Application.Common.Security;
using CampusBite.Infrastructure.Auth;
using CampusBite.Infrastructure.Persistence;
using CampusBite.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

    // JJ-Sprint1 02/10/2026: base de datos PostgreSQL con EF Core; el esquema lo controla Liquibase (db/)
    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Database no está configurada. Define ConnectionStrings__Database en el archivo .env o en las variables de entorno.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());
    }
}
