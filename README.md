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
  Entities/         Filas que devuelven los procedimientos y vistas de la BD.
  Dtos/<Modulo>/    Objetos de entrada/salida de la API (nunca exponer entidades directo).
  Enums/            RolUsuario coincide con el catálogo `rol` (id_rol).
Data/               AppDbContext: conexión a PostgreSQL (sin DbSet ni migraciones de EF).
db/                 Esquema oficial de la BD: migraciones SQL, seed y smoke test (ver db/README.md).
Common/
  Extensions/       Registro de servicios (BD, JWT, CORS, Swagger, inyección de dependencias).
  Middleware/       Manejo global de errores.
  Exceptions/       ApiException para errores de negocio (404, 409, 401, ...).
  Settings/         Clases que mapean la configuración (JwtSettings, CorsSettings).
```

### Base de datos
El esquema lo controlan los scripts de `db/`, **no** EF Core: no uses `dotnet ef migrations`.
La API se conecta con el usuario `app_backend`, que no puede leer tablas: solo ejecuta los procedimientos `sp_*`
y lee la vista `vw_menu_cafeteria`. Por eso los servicios consultan con `_context.Database.SqlQuery<T>(...)`
sobre ellos (ver `Services/AuthService.cs`). Si necesitas otra consulta, se agrega una migración nueva en
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
1. Clase en `Models/Entities/` con las columnas que devuelve la vista o el procedimiento.
2. DTOs en `Models/Dtos/Cafeterias/`.
3. Interfaz `Interfaces/ICafeteriaService.cs`.
4. Servicio `Services/CafeteriaService.cs : ICafeteriaService`.
5. Registrar en `Common/Extensions/ServiceCollectionExtensions.cs` → `AddApplicationServices`:
   `services.AddScoped<ICafeteriaService, CafeteriaService>();`
6. Controlador `Controllers/CafeteriasController.cs` que recibe `ICafeteriaService` por constructor.
7. Para errores de negocio lanza `ApiException.NotFound("...")`, etc. El middleware arma la respuesta.
8. Para proteger un endpoint usa `[Authorize]` o `[Authorize(Roles = "AdminSistema")]` (roles: `Alumno`, `AdminCafeteria`, `AdminSistema`).

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
# 1) Base de datos con Docker + Liquibase (detalles y comandos extra en db/README.md)
cd db
cp .env.example .env              # y cambia DB_PASSWORD
docker compose up -d db
docker compose run --rm liquibase
cd ..

# 2) Levantar la API. En .env: ConnectionStrings__Database con Username=app_backend;Password=app_backend_dev
dotnet run --launch-profile http
```

Si ya tienes PostgreSQL instalado en el puerto 5432, el contenedor `db` choca con él: cambia `DB_PORT` en
`db/.env` (por ejemplo, `5433`) y usa ese mismo puerto en `ConnectionStrings__Database`.

Swagger: http://localhost:5167/swagger — usa `POST /api/auth/login` y pega el token en **Authorize**.
