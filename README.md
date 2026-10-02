# CampusBite-Back
API de CampusBite: gestiona usuarios, autenticación, cafeterías, menús, pedidos, pagos y folios digitales con QR. Sirve los datos que la PWA guarda en caché y sincroniza los pedidos hechos con red inestable al recuperar la conexión. Envía notificaciones push cuando el pedido está listo. UTTT · 10 IDGSM G3.

## Stack
.NET 9 · ASP.NET Core Web API · EF Core + PostgreSQL · JWT Bearer · Swagger · CORS

## Estructura

```
Controllers/        Endpoints HTTP. Solo reciben la petición y llaman a la interfaz del servicio.
Interfaces/         Contratos (IAuthService, ITokenService, ...).
Services/           Implementan las interfaces; aquí vive la lógica de negocio.
Models/
  Entities/         Tablas de la base de datos (heredan de BaseEntity).
  Dtos/<Modulo>/    Objetos de entrada/salida de la API (nunca exponer entidades directo).
  Enums/
Data/               AppDbContext (DbSet y configuración de tablas) y Migrations.
Common/
  Extensions/       Registro de servicios (BD, JWT, CORS, Swagger, inyección de dependencias).
  Middleware/       Manejo global de errores.
  Exceptions/       ApiException para errores de negocio (404, 409, 401, ...).
  Settings/         Clases que mapean la configuración (JwtSettings, CorsSettings).
```

### Agregar un módulo nuevo (ej. Cafeterías)
1. Entidad en `Models/Entities/Cafeteria.cs` (hereda `BaseEntity`) y su `DbSet` en `Data/AppDbContext.cs`.
2. DTOs en `Models/Dtos/Cafeterias/`.
3. Interfaz `Interfaces/ICafeteriaService.cs`.
4. Servicio `Services/CafeteriaService.cs : ICafeteriaService`.
5. Registrar en `Common/Extensions/ServiceCollectionExtensions.cs` → `AddApplicationServices`:
   `services.AddScoped<ICafeteriaService, CafeteriaService>();`
6. Controlador `Controllers/CafeteriasController.cs` que recibe `ICafeteriaService` por constructor.
7. Para errores de negocio lanza `ApiException.NotFound("...")`, etc. El middleware arma la respuesta.
8. Para proteger un endpoint usa `[Authorize]` o `[Authorize(Roles = "Administrador")]`.

## Variables de entorno
Los secretos **no** van en `appsettings.json`. Se leen del archivo `.env` (local) o de las variables de entorno del servidor.

```bash
cp .env.example .env   # y llenar los valores reales
```

| Variable | Descripción |
|---|---|
| `ConnectionStrings__Database` | Cadena de conexión de PostgreSQL |
| `Jwt__Key` | Llave de firma del JWT (mínimo 32 caracteres) |
| `Jwt__Issuer` / `Jwt__Audience` | Emisor y audiencia del token |
| `Jwt__ExpirationMinutes` | Duración del token |
| `Cors__AllowedOrigins` | Orígenes del front separados por coma (vacío = cualquiera) |
| `Swagger__Enabled` | Muestra Swagger fuera de Development |

## Ejecutar

```bash
dotnet tool install --global dotnet-ef          # solo la primera vez
dotnet ef migrations add InitialCreate -o Data/Migrations
dotnet ef database update
dotnet run --launch-profile http
```

Swagger: http://localhost:5167/swagger — usa `POST /api/auth/login` y pega el token en **Authorize**.
