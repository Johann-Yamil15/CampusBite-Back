using System.Text;
using System.Threading.RateLimiting;
using CampusBite_Back.Common.Settings;
using CampusBite_Back.Data;
using CampusBite_Back.Interfaces;
using CampusBite_Back.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace CampusBite_Back.Common.Extensions;

// JJ-Sprint1 02/10/2026: registro de servicios agrupado por responsabilidad para mantener Program.cs limpio
public static class ServiceCollectionExtensions
{
    // JJ-Sprint1 02/10/2026: inyección de dependencias interfaz -> servicio (registrar aquí cada servicio nuevo)
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }

    // JJ-Sprint1 02/10/2026: base de datos relacional PostgreSQL con EF Core (tablas y columnas en snake_case)
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
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

        return services;
    }

    // JJ-Sprint1 02/10/2026: autenticación JWT Bearer validando emisor, audiencia, firma y expiración
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        services.Configure<JwtSettings>(jwtSection);

        var jwt = jwtSection.Get<JwtSettings>() ?? new JwtSettings();
        if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key no está configurada o es muy corta (mínimo 32 caracteres). Define Jwt__Key en el archivo .env o en las variables de entorno.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        return services;
    }

    // JJ-Sprint1 02/10/2026: CORS con orígenes configurables para la PWA (CampusBite-Front)
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var cors = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new CorsSettings();
        var origins = cors.GetOrigins();

        services.AddCors(options =>
        {
            options.AddPolicy(CorsSettings.PolicyName, policy =>
            {
                if (origins.Length > 0)
                {
                    policy.WithOrigins(origins).AllowCredentials();
                }
                else
                {
                    policy.AllowAnyOrigin();
                }

                policy.AllowAnyHeader().AllowAnyMethod();
            });
        });

        return services;
    }

    // JJ-Sprint1 02/10/2026: Swagger con botón "Authorize" para probar endpoints protegidos con JWT
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "CampusBite API",
                Version = "v1",
                Description = "API de CampusBite · UTTT 10 IDGSM G3"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Pega solo el token JWT (sin la palabra Bearer)."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    // JJ-Sprint2 09/10/2026: limita intentos de login/registro por IP (fuerza bruta) y responde 429 con ProblemDetails
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limites = configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>() ?? new RateLimitSettings();

        // En Azure la IP real del cliente llega en X-Forwarded-For; se toma solo la última entrada (la que agrega Azure)
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitSettings.PoliticaAuth, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limites.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(limites.AuthWindowSeconds),
                        QueueLimit = 0
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                await response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "TooManyRequests",
                    Detail = "Demasiados intentos. Espera un momento y vuelve a intentarlo.",
                    Instance = context.HttpContext.Request.Path
                }, options: null, contentType: "application/problem+json", cancellationToken);
            };
        });

        return services;
    }
}
