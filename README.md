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
db/                           Esquema de la BD: scripts por módulo, consolidado versionado y Liquibase local (ver db/README.md)
```

**Regla de dependencias** (el compilador la hace cumplir): `Api → Application → Domain ← Infrastructure`, y `Api`
conoce a `Infrastructure` solo para registrarla en `Program.cs`.
- Un servicio de Application **nunca** usa `AppDbContext`, SQL ni Npgsql: pide los datos a un repositorio (interfaz).
- Un controlador **nunca** tiene lógica: valida el DTO, llama al servicio y devuelve el resultado.
- Los errores de negocio se lanzan con las excepciones de Domain; `ExceptionMiddleware` es el único lugar que
  decide el código HTTP.

### Base de datos
El esquema lo controlan los scripts de `db/`, **no** EF Core: no uses `dotnet ef migrations`.
La API se conecta con el usuario `app_backend`, que no puede leer tablas ni vistas: solo ejecuta los
**procedimientos** `sp_*` con `CALL`. Los que devuelven algo lo hacen por parámetros `OUT` (se manda `NULL` en su
lugar y `CALL` regresa una fila); las listas llegan como `JSONB`. `Database.SqlQuery<T>` de EF no sirve para `CALL`,
así que los repositorios usan `NpgsqlCommand` sobre la conexión del `DbContext`
(ver `src/CampusBite.Infrastructure/Auth/UsuarioRepository.cs`).
Si necesitas otra consulta, se agrega un script nuevo en `db/scripts/06_procedimientos/`, su línea en el `orden.txt`
del módulo y su `GRANT` en `db/scripts/07_seguridad/001_privilegios.sql`; luego `./db/consolidado/build_schema.sh`.

### Qué hay en la base de datos
Versión `V1.0.0.004` (detalle, revisión de redundancia y de fechas en [`db/README.md`](db/README.md)).
Todas las llaves son `UUID` salvo `rol`. Las columnas `*_en` son `TIMESTAMPTZ` (UTC); los horarios son hora local
de `America/Mexico_City`.

**Tablas**

| Tabla | Columnas principales | Notas |
|---|---|---|
| `rol` | `id_rol`, `nombre` | Catálogo: 1 `alumno`, 2 `admin_cafeteria`, 3 `admin_sistema` (enum `RolUsuario`) |
| `usuario` | `id_usuario`, `id_rol`, `id_cafeteria`, `nombre`, `correo`, `contrasena_hash`, `matricula`, `telefono`, `activo`, `creado_en` | `correo` CITEXT único; `matricula` única; `id_cafeteria` solo para encargados (rol 2) |
| `cafeteria` | `id_cafeteria`, `nombre`, `ubicacion`, `es_del_campus`, `punto_entrega`, `activa` | Sus encargados están en `usuario.id_cafeteria` |
| `cafeteria_metodo_pago` | `id_cafeteria`, `metodo` | Un renglón por método aceptado (antes `acepta_*`) |
| `horario_cafeteria` | `id_cafeteria`, `dia_semana` (1 = lunes … 7), `hora_apertura`, `hora_cierre` | |
| `categoria` | `id_cafeteria`, `nombre` | Nombre único por cafetería |
| `producto` | `id_cafeteria`, `id_categoria`, `nombre`, `descripcion`, `precio`, `imagen_url`, `disponible` | Nombre único por cafetería; precio > 0; la cafetería no se cambia |
| `orden` | `id_usuario`, `id_cafeteria`, `estado`, `recogida_en`, `codigo_recogida` (6 dígitos), `notas`, `creado_en` | Sin `total`: el total es `pago.monto` |
| `detalle_orden` | `id_orden`, `id_producto`, `cantidad` (1–20), `precio_unitario`, `subtotal` (calculado) | Solo productos de la cafetería de la orden |
| `pago` | `id_orden` (1 por orden), `metodo`, `monto`, `estado`, `referencia`, `pagado_en` | `monto` = suma del detalle (lo revisa la BD al confirmar) |
| `historial_estado_orden` | `id_orden`, `estado`, `cambiado_por`, `cambiado_en` | Se llena solo con un trigger |
| `suscripcion_push` | `id_usuario`, `endpoint`, `llave_p256dh`, `llave_auth`, `creado_en` | Notificaciones push de la PWA |
| `bitacora` | `tabla`, `id_registro`, `operacion` (I/U/D), `datos_antes`, `datos_despues`, `cambiado_por`, `cambiado_en` | Historial de todo: la llenan triggers en cada alta, cambio y baja. Nunca guarda contraseñas |

Toda tabla tiene `creado_en` (y `actualizado_en` si sus filas se editan), salvo `historial_estado_orden` y `bitacora`, que usan `cambiado_en`.

**Estados (ENUM)**
- `estado_orden`: `pendiente` → `confirmada` → `en_preparacion` → `lista` → `entregada`; desde cualquiera antes de `entregada` se puede pasar a `cancelada`. La BD rechaza cualquier otro salto.
- `metodo_pago`: `tarjeta`, `transferencia`, `efectivo`.
- `estado_pago`: `pendiente`, `pagado`, `rechazado`, `reembolsado`, `cancelado`.

**Procedimientos que puede usar el backend** (todos `PROCEDURE`; en los `OUT` se manda `NULL`)

| Llamada | Devuelve (OUT) | Qué hace |
|---|---|---|
| `CALL sp_registrar_usuario(nombre, correo, hash, matricula, telefono, NULL)` | `p_id_usuario` | Crea un alumno. Correo o matrícula repetidos → error `23505` |
| `CALL sp_obtener_credenciales(correo, NULL, NULL, NULL, NULL)` | `p_id_usuario, p_id_rol, p_contrasena_hash, p_activo` | Para el login; todo `NULL` si el correo no existe |
| `CALL sp_menu_cafeteria(id_cafeteria o NULL, NULL)` | `p_menu` (JSON) | Cafeterías activas con `metodos_pago` y `productos` |
| `CALL sp_crear_orden(id_usuario, id_cafeteria, recogida_en, metodo, items, notas, NULL)` | `p_id_orden` | Orden + detalle + pago. `items`: `[{"id_producto":"<uuid>","cantidad":2}]`. Valida alumno activo, horario, recogida ≥ 5 min, método aceptado y productos. Efectivo queda `confirmada`; tarjeta o transferencia, `pendiente` |
| `CALL sp_confirmar_pago(id_orden, referencia)` | — | Tarjeta o transferencia: pago `pagado` y orden `confirmada` |
| `CALL sp_cambiar_estado_orden(id_orden, nuevo, id_actor)` | — | `en_preparacion`, `lista` o `cancelada`. Solo el encargado de esa cafetería o `admin_sistema` |
| `CALL sp_cancelar_orden(id_orden, id_usuario)` | — | El alumno cancela antes de que entre a preparación |
| `CALL sp_entregar_orden(id_orden, codigo, id_actor)` | — | Valida el código de recogida, cobra el efectivo y marca `entregada` |
| `CALL sp_mis_ordenes(id_usuario, limite, desplazamiento, NULL)` | `p_ordenes` (JSON) | Historial del alumno, más recientes primero; máximo 100 por página |
| `CALL sp_cola_cafeteria(id_cafeteria, id_actor, NULL)` | `p_cola` (JSON) | Pedidos activos por hora de recogida. Solo el encargado o `admin_sistema` (error `42501` si no) |
| `CALL sp_bitacora_registro(id_actor, tabla, id_registro, limite, NULL)` | `p_historial` (JSON) | Historial completo de un registro (qué cambió, quién y cuándo). Solo `admin_sistema` |

Al cancelar una orden, su pago pasa solo a `reembolsado` (si ya estaba pagado) o `cancelado`.
Las vistas `vw_*` existen, pero solo las usan los procedimientos: la API no tiene permiso para leerlas.

Lo que no aparece aquí (por ejemplo, el CRUD de cafeterías y productos, el perfil del usuario o las suscripciones push) todavía no tiene procedimiento: hay que agregarlo en un script nuevo.

## Agregar un módulo nuevo (ej. Cafeterías)
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

## Conexiones a la base de datos (local y producción)

La API puede trabajar con dos bases. Las dos usan el rol `app_backend`, que solo ejecuta los procedimientos `sp_*`
(no tiene acceso directo a tablas ni vistas).

| | Local (desarrollo) | Producción (nube) |
|---|---|---|
| Servidor | PostgreSQL en tu máquina, `localhost:5432` | Neon (PostgreSQL 18), región `us-east-2` |
| Host | `localhost` | `ep-bold-haze-b5zagdgx-pooler.c-7.us-east-2.aws.neon.tech` |
| Base | `campusbite` | `neondb` |
| Usuario de la API | `app_backend` | `app_backend` |
| Contraseña | `app_backend_dev` (pública, solo local) | Secreta: está en Azure y la da el responsable del backend |
| Conexión segura | No aplica | `SSL Mode=Require;Channel Binding=Require` |
| Cómo se crea el esquema | Liquibase en Docker (contexto `dev`) o el script consolidado local | Script consolidado `_prod` (`db/consolidado/versiones`) con `psql`, desde una PC del equipo. Liquibase no se usa en producción |
| Datos | De prueba (seed): 3 usuarios, 1 cafetería y 3 productos | Reales; sin datos de prueba |
| Variable en `.env` | `ConnectionStrings__Local` | `ConnectionStrings__Produccion` |

**Cómo elegir la base al correr la API en tu máquina.** `Database__Conexion` decide cuál cadena se usa, y los
perfiles de arranque ya la fijan:

| Perfil (Visual Studio o `--launch-profile`) | Base |
|---|---|
| `http` (el de `dotnet run`) / `https` | Local |
| `https (BD produccion)` | Producción. **Los registros y cambios son reales**: úsalo solo para revisar un problema |

Al arrancar, la consola muestra a qué base se conectó y avisa con `warn` cuando es producción:

```
info: Base de datos local (Local): campusbite en localhost, usuario app_backend
warn: Base de datos de PRODUCCIÓN (Produccion): neondb en ep-bold-haze-...neon.tech, usuario app_backend. Los cambios son reales.
```

**En Azure** no se usa `Database__Conexion`: la API toma `ConnectionStrings__Database` de las variables del
App Service. Si no hay selector, siempre se usa esa variable, así que en Azure no hay que cambiar nada.

**Reglas**
- La contraseña de producción y la de `neondb_owner` (dueño de la base, solo para actualizar el esquema) **nunca** van al repo,
  al README ni a chats. Se comparten en persona o por un gestor de contraseñas.
- Para probar algo que escribe datos, usa la base local. Si necesitas datos reales, pide un respaldo.

## CORS: qué fronts pueden llamar a la API

Los orígenes no son secretos, así que van en `appsettings` según el ambiente:

| Ambiente | Archivo | Fronts permitidos |
|---|---|---|
| Development (tu máquina) | `appsettings.Development.json` | `http://localhost:4200`, `https://localhost:4200` y el front de producción |
| Production (Azure) | `appsettings.json` | Solo `https://campusbite-front-awbkd6becga6c7aw.centralus-01.azurewebsites.net` |

- En local, el front Angular (`ng serve`, puerto 4200) llama a `/api` y su `proxy.conf.json` lo manda a
  `https://localhost:7254`, así que normalmente ni pasa por CORS. Para eso, el back debe correr con el perfil `https`.
- El origen va **sin "/" final** (si se pone, la API lo quita).
- Para reemplazar la lista en cualquier ambiente: variable `Cors__AllowedOrigins` (separados por coma). Si en
  Azure existe esa variable, manda sobre `appsettings.json`: bórrala o pon ahí la URL del front.
- Si un front nuevo cambia de URL, se actualiza `appsettings.json` y se despliega.
- Al arrancar, la consola muestra la lista: `CORS permite: ...`.

## Variables de entorno
Los secretos **no** van en `appsettings.json`. Se leen del archivo `.env` (local) o de las variables de entorno del servidor.

```bash
cp .env.example .env   # y llenar los valores reales
```

| Variable | Descripción |
|---|---|
| `Database__Conexion` | `Local` o `Produccion`: cuál de las dos cadenas usar en tu máquina (los perfiles ya la fijan) |
| `ConnectionStrings__Local` / `ConnectionStrings__Produccion` | Cadenas de la base local y de Neon |
| `ConnectionStrings__Database` | Cadena que usa Azure (cuando no hay `Database__Conexion`) |
| `Jwt__Key` | Llave de firma del JWT (mínimo 32 caracteres) |
| `Jwt__Issuer` / `Jwt__Audience` | Emisor y audiencia del token |
| `Jwt__ExpirationMinutes` | Duración del token |
| `Cors__AllowedOrigins` | Opcional: reemplaza los fronts permitidos de `appsettings` (separados por coma) |
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

# 2) Levantar la API con la base local (ConnectionStrings__Local en .env)
dotnet run --project src/CampusBite.Api --launch-profile https    # https://localhost:7254 (el que usa el proxy del front)

# 3) Pruebas (también corren en GitHub Actions antes de cada despliegue)
dotnet test
```

En Visual Studio abre `CampusBite-Back.sln` y marca `CampusBite.Api` como proyecto de inicio.

**Sin Docker:** si ya tienes PostgreSQL instalado, usa el script consolidado y versionado
(`V1.0.0.NNN`, un solo archivo idempotente con esquema y datos de prueba). Instrucciones en
[`db/consolidado/README.md`](db/consolidado/README.md).

Si ya tienes PostgreSQL instalado en el puerto 5432, el contenedor `db` choca con él: cambia `DB_PORT` en
`db/.env` (por ejemplo, `5433`) y usa ese mismo puerto en `ConnectionStrings__Local`.

Swagger: https://localhost:7254/swagger (o http://localhost:5167/swagger con el perfil `http`) — usa `POST /api/auth/login` y pega el token en **Authorize**.

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
