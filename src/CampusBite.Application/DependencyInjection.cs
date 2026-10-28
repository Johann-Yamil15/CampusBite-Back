using CampusBite.Application.Auth;
using CampusBite.Application.Cafeterias;
using CampusBite.Application.Productos;
using Microsoft.Extensions.DependencyInjection;

namespace CampusBite.Application;

// JJ-Sprint2 09/10/2026: registra los servicios (casos de uso) de cada módulo; agregar aquí cada módulo nuevo
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Auth
        services.AddScoped<IAuthService, AuthService>();

        // Cafeterías (TEC-08)
        services.AddScoped<ICafeteriaService, CafeteriaService>();

        // Productos: alta (TEC-09) e imagen (TEC-10)
        services.AddScoped<IProductoService, ProductoService>();

        return services;
    }
}
