# CampusBite-Back
API de CampusBite: gestiona usuarios, autenticación, cafeterías, menús, pedidos, pagos y folios digitales con QR. Sirve los datos que la PWA guarda en caché y sincroniza los pedidos hechos con red inestable al recuperar la conexión. Envía notificaciones push cuando el pedido está listo. UTTT · 10 IDGSM G3.

## Stack
.NET 9 · ASP.NET Core Web API · EF Core + PostgreSQL · JWT Bearer · Swagger · CORS

## Estructura: arquitectura limpia por módulos

Cuatro proyectos (capas) y, dentro de cada uno, una carpeta por módulo (`Auth/`, `Cafeterias/`, `Ordenes/`...).
Cada módulo es una "rebanada vertical" que atraviesa las cuatro capas.

```
CampusBite-Back.sln
src/
  CampusBite.Domain/          Núcleo del negocio. No depende de nada.
    Auth/                     RolUsuario
    Common/Exceptions/        NotFound, Conflict, Unauthorized, Forbidden, BusinessRule (sin HTTP)
  CampusBite.Application/     Casos de uso. Depende solo de Domain (sin EF, Npgsql, BCrypt ni ASP.NET).
    Auth/                     IAuthService, AuthService, IUsuarioRepository, Dtos/
    Common/Security/          IPasswordHasher, ITokenService (contratos)
    DependencyInjection.cs    AddApplication(): registra los servicios de cada módulo
  CampusBite.Infrastructure/  Detalles técnicos que implementan los contratos de Application.
    Auth/                     UsuarioRepository (llama a los sp_* de la BD)
    Persistence/              AppDbContext (conexión a PostgreSQL)
    Security/                 BCryptPasswordHasher, TokenService (JWT), JwtSettings
    DependencyInjection.cs    AddInfrastructure(): BD, repositorios, BCrypt y JWT
  CampusBite.Api/             Capa web: controladores, middleware y Program.cs.
    Auth/                     AuthController
    Common/                   Middleware (errores), Extensions (JWT, CORS, Swagger, límite de intentos), Settings
tests/
  CampusBite.Application.Tests/  Pruebas de los servicios con dobles (sin base de datos)
db/                           Esquema oficial de la BD con Liquibase (ver db/README.md)
```

**Regla de dependencias** (el compilador la hace cumplir): `Api → Application → Domain ← Infrastructure`, y `Api`
conoce a `Infrastructure` solo para registrarla en `Program.cs`.
- Un servicio de Application **nunca** usa `AppDbContext`, SQL ni Npgsql: pide los datos a un repositorio (interfaz).
- Un controlador **nunca** tiene lógica: valida el DTO, llama al servicio y devuelve el resultado.
- Los errores de negocio se lanzan con las excepciones de Domain; `ExceptionMiddleware` es el único lugar que
  decide el código HTTP.

### Base de datos
El esquema lo controlan los scripts de `db/`, **no** EF Core: no uses `dotnet ef migrations`.
La API se conecta con el usuario `app_backend`, que no puede leer tablas: solo ejecuta los procedimientos `sp_*`
y lee la vista `vw_menu_cafeteria`. Por eso los repositorios de Infrastructure consultan con
`_context.Database.SqlQuery<T>(...)` sobre ellos (ver `src/CampusBite.Infrastructure/Auth/UsuarioRepository.cs`).
Si necesitas otra consulta, se agrega una migración nueva en
`db/changelog/migrations/V00N__...sql`, con su `changeSet` en `changelog-master.xml` y su `GRANT` a `app_backend`.

### Qué hay en la base de datos
Resumen de `db/changelog/migrations` (V001–V005). Todas las llaves son `UUID` salvo `rol`.
Las fechas son `TIMESTAMPTZ` (UTC); los horarios se evalúan en `America/Mexico_City`.

**Tablas**

| Tabla | Columnas principales | Notas |
|---|---|---|
| `rol` | `id_rol`, `nombre` | Catálogo: 1 `alumno`, 2 `admin_cafeteria`, 3 `admin_sistema` (enum `RolUsuario`) |
| `usuario` | `id_usuario`, `id_rol`, `nombre`, `correo`, `contrasena_hash`, `matricula`, `telefono`, `activo` | `correo` no distingue mayúsculas (CITEXT) y es único; `matricula` única; teléfono de 10 a 15 dígitos |
| `cafeteria` | `id_cafeteria`, `id_admin` → usuario, `nombre`, `ubicacion`, `es_del_campus`, `punto_entrega`, `acepta_efectivo`, `acepta_tarjeta`, `acepta_transferencia`, `activa` | Debe aceptar al menos un método de pago |
| `horario_cafeteria` | `id_cafeteria`, `dia_semana` (1 = lunes … 7), `hora_apertura`, `hora_cierre` | |
| `categoria` | `id_cafeteria`, `nombre` | Nombre único por cafetería |
| `producto` | `id_cafeteria`, `id_categoria`, `nombre`, `descripcion`, `precio`, `imagen_url`, `disponible` | Nombre único por cafetería; precio > 0 |
| `orden` | `id_usuario`, `id_cafeteria`, `estado`, `hora_recogida`, `codigo_recogida` (6 dígitos), `total`, `notas` | |
| `detalle_orden` | `id_orden`, `id_producto`, `cantidad` (1–20), `precio_unitario`, `subtotal` (calculado) | Solo productos de la cafetería de la orden |
| `pago` | `id_orden` (1 por orden), `metodo`, `monto`, `estado`, `referencia`, `pagado_en` | |
| `historial_estado_orden` | `id_orden`, `estado`, `cambiado_por`, `cambiado_en` | Se llena solo con un trigger |
| `suscripcion_push` | `id_usuario`, `endpoint`, `llave_p256dh`, `llave_auth` | Notificaciones push de la PWA |

**Estados (ENUM)**
- `estado_orden`: `pendiente` → `confirmada` → `en_preparacion` → `lista` → `entregada`; desde cualquiera antes de `entregada` se puede pasar a `cancelada`. La BD rechaza cualquier otro salto.
- `metodo_pago`: `tarjeta`, `transferencia`, `efectivo`.
- `estado_pago`: `pendiente`, `pagado`, `rechazado`, `reembolsado`, `cancelado`.

**Procedimientos que puede usar el backend**

| Procedimiento | Devuelve | Qué hace |
|---|---|---|
| `sp_registrar_usuario(nombre, correo, hash, matricula?, telefono?)` | `UUID` | Crea un alumno. Correo o matrícula repetidos → error `23505` |
| `sp_obtener_credenciales(correo)` | `id_usuario, id_rol, contrasena_hash, activo` | Para el login; el hash se compara en el backend con BCrypt |
| `sp_crear_orden(id_usuario, id_cafeteria, hora_recogida, metodo, items, notas?)` | `UUID` | Crea orden + detalles + pago. `items` es JSON `[{"id_producto":"<uuid>","cantidad":2}]`. Valida: alumno activo, cafetería abierta a esa hora, recogida ≥ 5 min en el futuro, método aceptado y productos disponibles. Efectivo queda `confirmada`; tarjeta o transferencia queda `pendiente` |
| `sp_confirmar_pago(id_orden, referencia)` | — | Tarjeta o transferencia: marca el pago como `pagado` y la orden como `confirmada` |
| `sp_cambiar_estado_orden(id_orden, nuevo, id_actor)` | — | La cafetería avanza a `en_preparacion`, `lista` o `cancelada`. Solo el admin de esa cafetería o `admin_sistema` |
| `sp_cancelar_orden(id_orden, id_usuario)` | — | El alumno cancela su orden antes de que entre a preparación |
| `sp_entregar_orden(id_orden, codigo, id_actor)` | — | Valida el código de recogida, cobra el efectivo y marca `entregada` |
| `sp_mis_ordenes(id_usuario, limite = 20, desplazamiento = 0)` | filas de `vw_mis_ordenes` | Historial del alumno, más recientes primero; máximo 100 filas por página |
| `sp_cola_cafeteria(id_cafeteria, id_actor)` | filas de `vw_cola_cafeteria` | Pedidos activos de la cafetería ordenados por hora de recogida. Solo el admin de esa cafetería o `admin_sistema` (error `42501` si no) |

Al cancelar una orden, su pago pasa solo a `reembolsado` (si ya estaba pagado) o `cancelado`.

**Vistas** (V004 dejó a la API leer directo solo `vw_menu_cafeteria`; las otras dos se leen con su `sp_*`)

| Vista | Uso | Columnas |
|---|---|---|
| `vw_menu_cafeteria` | Menú de cafeterías activas (solo productos disponibles). Lectura directa | `id_cafeteria, cafeteria, es_del_campus, punto_entrega, categoria, id_producto, producto, descripcion, precio, imagen_url` |
| `vw_cola_cafeteria` | Pantalla de pedidos de la cafetería (sin código de recogida). Vía `sp_cola_cafeteria` | `id_orden, id_cafeteria, estado, hora_recogida, notas, alumno, productos (JSON), total, metodo, estado_pago` |
| `vw_mis_ordenes` | Historial del alumno. Vía `sp_mis_ordenes` | `id_orden, id_usuario, cafeteria, punto_entrega, estado, hora_recogida, codigo_recogida, total, metodo, estado_pago, creada_en` |

Lo que no aparece aquí (por ejemplo, el CRUD de cafeterías y productos, el perfil del usuario o las suscripciones push) todavía no tiene procedimiento o vista con permiso para `app_backend`: hay que agregarlo en una migración nueva.

### Agregar un módulo nuevo (ej. Cafeterías)
Se crea la misma carpeta `Cafeterias/` en cada capa que la necesite, de adentro hacia afuera:

| # | Capa | Archivo | Qué contiene |
|---|---|---|---|
| 1 | Domain | `Cafeterias/` (solo si hace falta) | Enums o reglas propias del módulo |
| 2 | Application | `Cafeterias/Dtos/*.cs` | Entrada y salida de la API, con validaciones y mensajes en español |
| 3 | Application | `Cafeterias/ICafeteriaRepository.cs` + la clase de lo que devuelve | Qué datos necesita el módulo (sin SQL) |
| 4 | Application | `Cafeterias/ICafeteriaService.cs` + `CafeteriaService.cs` | Reglas del negocio; usa el repositorio |
| 5 | Application | `DependencyInjection.cs` | `services.AddScoped<ICafeteriaService, CafeteriaService>();` |
| 6 | Infrastructure | `Cafeterias/CafeteriaRepository.cs` | SQL sobre los `sp_*` o `vw_*`; traduce errores de PostgreSQL a excepciones de Domain |
| 7 | Infrastructure | `DependencyInjection.cs` | `services.AddScoped<ICafeteriaRepository, CafeteriaRepository>();` |
| 8 | Api | `Cafeterias/CafeteriasController.cs` | Recibe `ICafeteriaService` por constructor; pasa el `CancellationToken` |
| 9 | Tests | `Cafeterias/CafeteriaServiceTests.cs` | Pruebas del servicio con un repositorio falso |

- Errores de negocio: `throw new NotFoundException("...")`, `ConflictException`, `ForbiddenException`, etc.
- Proteger un endpoint: `[Authorize]` o `[Authorize(Roles = "AdminSistema")]` (roles: `Alumno`, `AdminCafeteria`, `AdminSistema`).
- Usa el módulo `Auth/` de cada capa como ejemplo a copiar.

## API de autenticación
Base: `/api/auth`. Todas las respuestas son JSON; los errores usan el formato ProblemDetails
(`status`, `title`, `detail` y, en errores de validación, `errors` con un mensaje por campo).

| Método y ruta | Auth | Cuerpo | Respuestas |
|---|---|---|---|
| `POST /api/auth/registro` | No | `nombre`, `correo`, `password`, `matricula?`, `telefono?` | 201 · 400 · 409 · 429 |
| `POST /api/auth/login` | No | `correo`, `password` | 200 · 400 · 401 · 429 |
| `GET /api/auth/me` | Bearer | — | 200 · 401 |

**Registro**: crea siempre un `Alumno` y ya devuelve el token, así que el front puede entrar directo sin pedir login.

```json
// POST /api/auth/registro
{ "nombre": "Ana López", "correo": "ana@uttt.edu.mx", "password": "Password123",
  "matricula": "23300099", "telefono": "7711234567" }
```

**Respuesta de registro (201) y login (200)**:

```json
{ "usuarioId": "1ffc232e-0d4d-4206-9362-87a949b4342a", "correo": "ana@uttt.edu.mx",
  "rol": "Alumno", "token": "eyJhbGciOi...", "expiraEn": "2026-10-09T19:13:03Z" }
```

**`/me` (200)**: `{ "id": "...", "correo": "...", "rol": "Alumno" }`.

| Código | Cuándo | Qué mostrar en el front |
|---|---|---|
| 400 | Datos inválidos | El mensaje de cada campo en `errors` (ya vienen en español) |
| 401 | Login: correo o contraseña incorrectos · `/me`: sin token o token vencido | Login: el `detail`. Ruta protegida: mandar al login |
| 409 | Correo o matrícula ya registrados | El `detail` |
| 429 | Demasiados intentos desde la misma IP (20 por minuto por defecto) | El `detail`; el encabezado `Retry-After` indica los segundos de espera |

Reglas de registro: nombre de 2 a 80 caracteres, correo válido de máximo 120, contraseña de 8 a 72,
matrícula de máximo 20 y teléfono de 10 a 15 dígitos (los dos últimos son opcionales).

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
| `RateLimit__AuthPermitLimit` / `RateLimit__AuthWindowSeconds` | Intentos de login/registro por IP y ventana en segundos (opcional; 20 por 60 s) |

## Ejecutar

```bash
# 1) Base de datos con Docker + Liquibase (detalles y comandos extra en db/README.md)
cd db
cp .env.example .env              # y cambia DB_PASSWORD
docker compose up -d db
docker compose run --rm liquibase
cd ..

# 2) Levantar la API. En .env: ConnectionStrings__Database con Username=app_backend;Password=app_backend_dev
dotnet run --project src/CampusBite.Api --launch-profile http

# 3) Pruebas (también corren en GitHub Actions antes de cada despliegue)
dotnet test
```

En Visual Studio abre `CampusBite-Back.sln` y marca `CampusBite.Api` como proyecto de inicio.

**Sin Docker:** si ya tienes PostgreSQL instalado, usa el script consolidado y versionado
(`V1.0.0.NNN`, un solo archivo con esquema y datos de prueba). Instrucciones en
[`db/consolidado/README.md`](db/consolidado/README.md).

Si ya tienes PostgreSQL instalado en el puerto 5432, el contenedor `db` choca con él: cambia `DB_PORT` en
`db/.env` (por ejemplo, `5433`) y usa ese mismo puerto en `ConnectionStrings__Database`.

Swagger: http://localhost:5167/swagger — usa `POST /api/auth/login` y pega el token en **Authorize**.

## Contraseñas y token JWT
- **Contraseñas:** se cifran con BCrypt, costo 12 (`src/CampusBite.Infrastructure/Security/BCryptPasswordHasher.cs`). La base solo guarda el hash.
  El login tarda lo mismo exista o no el correo, para no revelar qué correos están registrados.
- **Token:** JWT firmado con HS256, dura `Jwt__ExpirationMinutes` (60 por defecto). Se envía en cada petición protegida como
  `Authorization: Bearer <token>`. Contenido (payload) que puede leer el front:

```json
{
  "sub": "1ffc232e-0d4d-4206-9362-87a949b4342a",
  "email": "alumno@uttt.edu.mx",
  "role": "Alumno",
  "jti": "id único del token",
  "iat": 1760000000, "nbf": 1760000000, "exp": 1760003600,
  "iss": "CampusBite.API", "aud": "CampusBite.PWA"
}
```

  - `role` es `Alumno`, `AdminCafeteria` o `AdminSistema`; los guards del front y `[Authorize(Roles = ...)]` usan esos nombres.
  - `exp` está en segundos UTC: el front debe mandar al login cuando se venza (la API responde 401).
  - Leer el payload en el front sirve para mostrar u ocultar pantallas; la seguridad real la valida siempre la API.
