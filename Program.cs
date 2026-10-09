using CampusBite_Back.Common.Extensions;
using CampusBite_Back.Common.Middleware;
using CampusBite_Back.Common.Settings;
using Microsoft.AspNetCore.Mvc;

// FB-Sprint1 02/10/2026: carga el archivo .env (si existe) sin sobrescribir variables de entorno reales
DotNetEnv.Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// FB-Sprint1 02/10/2026: asigna el puerto dinámico para despliegues en la nube
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// JJ-Sprint2 09/10/2026: errores de validación (400) con título en español y el mismo formato ProblemDetails
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Datos inválidos",
            Detail = "Revisa los campos indicados en errors.",
            Instance = context.HttpContext.Request.Path
        };

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    };
});

// FB-Sprint1 02/10/2026: inyecta la configuración del pool de PostgreSQL
builder.Services.AddDatabase(builder.Configuration);

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCorsPolicy(builder.Configuration);
builder.Services.AddSwaggerDocumentation();
builder.Services.AddApplicationServices();
builder.Services.AddAuthRateLimiting(builder.Configuration);

var app = builder.Build();

// JJ-Sprint2 09/10/2026: toma la IP real del cliente detrás del proxy de Azure (la usa el límite de intentos)
app.UseForwardedHeaders();

app.UseMiddleware<ExceptionMiddleware>();

// FB-Sprint1 02/10/2026: Swagger activo en Development o cuando Swagger__Enabled=true
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CampusBite API v1"));
}

app.UseHttpsRedirection();

app.UseCors(CorsSettings.PolicyName);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
