using CampusBite.Application.Auth;
using CampusBite.Application.Cafeterias;
using CampusBite.Application.Common.Security;
using CampusBite.Application.Alumnos;
using CampusBite.Application.Menu;
using CampusBite.Application.Negocio;
using CampusBite.Application.Ordenes;
using CampusBite.Infrastructure.Alumnos;
using CampusBite.Infrastructure.Negocio;
using CampusBite.Infrastructure.Ordenes;
using CampusBite.Application.Productos;
using CampusBite.Infrastructure.Auth;
using CampusBite.Infrastructure.Blob;
using CampusBite.Infrastructure.Correo;
using CampusBite.Infrastructure.Seguridad;
using CampusBite.Application.Common.Correo;
using CampusBite.Infrastructure.Cafeterias;
using CampusBite.Infrastructure.Menu;
using CampusBite.Infrastructure.Persistence;
using CampusBite.Infrastructure.Productos;
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
        services.AddScoped<IProductoRepository, ProductoRepository>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        // JJ-Sprint5 13/11/2026
        services.AddScoped<IOrdenRepository, OrdenRepository>();
        services.AddScoped<IAlumnoRepository, AlumnoRepository>();
        services.AddScoped<INegocioRepository, NegocioRepository>();

        // JY-Sprint4 27/10/2026: TEC-10, imágenes de productos en Azure Blob Storage. Singleton: el cliente de Azure
        // es seguro entre hilos y reutiliza sus conexiones
        services.Configure<AzureBlobSettings>(configuration.GetSection(AzureBlobSettings.SectionName));
        services.AddSingleton<IBlobService, AzureBlobService>();

        // JY-Sprint4 31/10/2026: correos de verificación por SMTP (MailKit)
        services.Configure<CorreoSettings>(configuration.GetSection(CorreoSettings.SectionName));
        services.AddSingleton<IEnviadorCorreo, SmtpEnviadorCorreo>();

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

        // JY-Sprint4 31/10/2026: a una BD remota solo se conecta con TLS
        connectionString = Tls.ExigirSslEnBd(connectionString);
        var datos = new NpgsqlConnectionStringBuilder(connectionString);
        services.AddSingleton(new DatabaseInfo(nombre, datos.Host ?? "?", datos.Database ?? "?", datos.Username ?? "?"));

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());
    }
}
