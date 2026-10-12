using CampusBite.Application.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace CampusBite.Application;

// JJ-Sprint2 09/10/2026: registra los servicios (casos de uso) de cada módulo; agregar aquí cada módulo nuevo
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Auth
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
