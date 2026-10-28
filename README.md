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
    Cafeterias/               MetodoPago
    Common/Exceptions/        NotFound, Conflict, Unauthorized, Forbidden, BusinessRule (sin HTTP)
  CampusBite.Application/     Casos de uso. Depende solo de Domain (sin EF, Npgsql, BCrypt ni ASP.NET).
    Auth/                     IAuthService, AuthService, IUsuarioRepository, Dtos/
    Cafeterias/               ICafeteriaService, CafeteriaService, ICafeteriaRepository, DatosCafeteria, Dtos/
    Common/Security/          IPasswordHasher, ITokenService, IUsuarioActual (contratos)
    DependencyInjection.cs    AddApplication(): registra los servicios de cada módulo
  CampusBite.Infrastructure/  Detalles técnicos que implementan los contratos de Application.
    Auth/                     UsuarioRepository (llama a los sp_* de la BD)
    Cafeterias/               CafeteriaRepository
    Persistence/              AppDbContext y Procedimientos (CALL a los sp_*, lectura de JSON y traducción de errores)
    Security/                 BCryptPasswordHasher, TokenService (JWT), JwtSettings
    DependencyInjection.cs    AddInfrastructure(): BD, repositorios, BCrypt y JWT
  CampusBite.Api/             Capa web: controladores, middleware y Program.cs.
    Auth/                     AuthController
    Cafeterias/               CafeteriasController
    Common/Middleware/        ExceptionMiddleware y ProblemaHttp (formato único de errores)
    Common/Security/          PoliticasAutorizacion (protección por rol) y UsuarioActual (quién llama, desde el JWT)
    Common/                   Extensions (JWT, CORS, Swagger, límite de intentos), Settings
tests/
  CampusBite.Application.Tests/  Pruebas de los servicios con dobles (sin base de datos)
  CampusBite.Api.Tests/          Pruebas de integración de la API en memoria (JWT, roles, errores)
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
| `CALL sp_listar_cafeterias(id_actor, incluir_inactivas, NULL)` | `p_cafeterias` (JSON) | Cafeterías por nombre; las inactivas solo `admin_sistema` |
| `CALL sp_obtener_cafeteria(id_cafeteria, id_actor, NULL)` | `p_cafeteria` (JSON o `NULL`) | Una cafetería; `NULL` si no existe o es inactiva y el actor no la administra |
| `CALL sp_crear_cafeteria(id_actor, nombre, ubicacion, es_del_campus, punto_entrega, metodos[], NULL)` | `p_id_cafeteria` | Alta con métodos de pago. Solo `admin_sistema`. Nombre repetido → `23505` |
| `CALL sp_actualizar_cafeteria(id_cafeteria, id_actor, nombre, ubicacion, es_del_campus, punto_entrega, metodos[])` | — | Edita y reemplaza los métodos. Su encargado o `admin_sistema` |
| `CALL sp_cambiar_activa_cafeteria(id_cafeteria, activa, id_actor)` | — | Baja lógica o reactivación. Solo `admin_sistema`. Con órdenes en curso → `55006` |
| `CALL sp_crear_producto(id_producto, id_cafeteria, id_actor, id_categoria, nombre, descripcion, precio, disponible, imagen_url)` | — | Alta en el menú; `id_producto` lo genera la API (la imagen se sube antes con ese id) e `imagen_url` puede ser `NULL`. Su encargado o `admin_sistema`. Nombre repetido en la cafetería (sin distinguir mayúsculas) → `23505` |
| `CALL sp_obtener_producto(id_cafeteria, id_producto, id_actor, NULL)` | `p_producto` (JSON o `NULL`) | Un producto con el nombre de su categoría; `NULL` si no existe, es de otra cafetería, o no está disponible / su cafetería está inactiva y el actor no la administra |
| `CALL sp_actualizar_imagen_producto(id_cafeteria, id_producto, id_actor, imagen_url, NULL)` | `p_imagen_anterior` | Guarda la URL de la imagen y devuelve la que tenía antes (para borrar ese blob). Su encargado o `admin_sistema`; producto de otra cafetería → `P0002` |

Al cancelar una orden, su pago pasa solo a `reembolsado` (si ya estaba pagado) o `cancelado`.
Las vistas `vw_*` existen, pero solo las usan los procedimientos: la API no tiene permiso para leerlas.

Lo que no aparece aquí (por ejemplo, listar, editar o borrar productos, el perfil del usuario o las suscripciones push) todavía no tiene procedimiento: hay que agregarlo en un script nuevo.

## Agregar un módulo nuevo (ej. Cafeterías)
`Cafeterias/` (TEC-08) ya está hecho siguiendo estos pasos: úsalo como ejemplo completo de CRUD.
Se crea la misma carpeta en cada capa que la necesite, de adentro hacia afuera:

| # | Capa | Archivo | Qué contiene |
|---|---|---|---|
| 1 | Domain | `Cafeterias/` (solo si hace falta) | Enums o reglas propias del módulo |
| 2 | Application | `Cafeterias/Dtos/*.cs` | Entrada y salida de la API, con validaciones y mensajes en español |
| 3 | Application | `Cafeterias/ICafeteriaRepository.cs` + la clase de lo que devuelve | Qué datos necesita el módulo (sin SQL) |
| 4 | Application | `Cafeterias/ICafeteriaService.cs` + `CafeteriaService.cs` | Reglas del negocio; usa el repositorio |
| 5 | Application | `DependencyInjection.cs` | `services.AddScoped<ICafeteriaService, CafeteriaService>();` |
| 6 | Infrastructure | `Cafeterias/CafeteriaRepository.cs` | `CALL` a los `sp_*` con `_context.EjecutarAsync(...)` (`Persistence/Procedimientos.cs`), que ya traduce los errores de PostgreSQL a excepciones de Domain |
| 7 | Infrastructure | `DependencyInjection.cs` | `services.AddScoped<ICafeteriaRepository, CafeteriaRepository>();` |
| 8 | Api | `Cafeterias/CafeteriasController.cs` | Recibe `ICafeteriaService` por constructor; pasa el `CancellationToken` |
| 9 | Tests | `Cafeterias/CafeteriaServiceTests.cs` y `Api.Tests/Cafeterias/` | Servicio con un repositorio falso; endpoints (roles, validación, errores) con un servicio falso |
| 0 | BD | `db/scripts/06_procedimientos/NNN_sp_*.sql` | Los procedimientos, su `GRANT` en `07_seguridad/001_privilegios.sql` y una prueba en `db/tests/smoke_test.sql` |

- Errores de negocio: `throw new NotFoundException("...")`, `ConflictException`, `ForbiddenException`, etc.
- Proteger un endpoint: ver [Protección de rutas por rol](#protección-de-rutas-por-rol-tec-07). Todo endpoint exige sesión salvo que diga `[AllowAnonymous]`.
- Usa el módulo `Cafeterias/` de cada capa como ejemplo a copiar.

## Protección de rutas por rol (TEC-07)

Todo pasa por el middleware de autenticación y autorización de ASP.NET Core (`UseAuthentication` y
`UseAuthorization` en `Program.cs`), configurado en `Api/Common/Security/PoliticasAutorizacion.cs`.

| Cómo se marca el endpoint | Quién entra |
|---|---|
| *(sin atributo)* | Cualquiera con sesión: **denegar por defecto**, un endpoint nuevo nunca queda público por olvido |
| `[AllowAnonymous]` | Cualquiera, sin token (solo `registro` y `login`) |
| `[Authorize(Policy = PoliticasAutorizacion.SoloAlumno)]` | `Alumno` (hace pedidos) |
| `[Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]` | `AdminCafeteria` y `AdminSistema` (atienden pedidos) |
| `[Authorize(Policy = PoliticasAutorizacion.SoloAdminSistema)]` | `AdminSistema` |

```csharp
[Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
[HttpPost("{idOrden:guid}/estado")]
public async Task<IActionResult> CambiarEstado(Guid idOrden, CambiarEstadoDto dto, CancellationToken ct)
{
    await _ordenes.CambiarEstadoAsync(idOrden, dto.Estado, ct);   // el servicio toma el actor de IUsuarioActual
    return NoContent();
}
```

Reglas:
- **Quién actúa sale del token, nunca de la petición.** Los servicios piden `IUsuarioActual` (Application) y le
  pasan `Id` a los procedimientos como `p_id_usuario` / `p_id_actor`. No se acepta un `idUsuario` en el body, la
  query o la ruta para decidir quién es.
- **El rol del token es la primera barrera, la base de datos es la segunda.** Ser `AdminCafeteria` no basta para
  tocar *cualquier* cafetería: los procedimientos revisan en la BD que el usuario siga activo, su rol actual y que
  sea encargado de *esa* cafetería (`sp_exigir_gestor_cafeteria`). Si la BD responde `42501`, el repositorio lanza
  `ForbiddenException` (403).
- El token dura `Jwt__ExpirationMinutes`. Si a alguien se le quita un rol o se desactiva, el token que ya tiene
  sigue pasando la primera barrera hasta que vence, pero la BD lo frena en cada procedimiento.
- 401 (sin sesión o token vencido) y 403 (rol sin permiso) responden con ProblemDetails, igual que los demás errores.
  Si el token venció, el `detail` lo dice y el encabezado `WWW-Authenticate` trae `invalid_token`: el front puede
  mandar al login. No se explica por qué un token es inválido (firma, emisor): eso solo ayudaría a un atacante.

| Código | Cuándo | Qué hacer en el front |
|---|---|---|
| 401 | Sin token, token inválido o vencido | Borrar la sesión y mandar al login |
| 403 | Hay sesión, pero el rol (o la BD) no permite la acción | Mostrar el `detail`; no mandar al login |

## API de cafeterías (TEC-08)
Base: `/api/cafeterias`. Todas piden sesión. "Eliminar" es una **baja lógica**: la cafetería deja de aparecer y de
aceptar órdenes, pero sus órdenes, productos y bitácora se conservan. Se puede reactivar.

| Método y ruta | Quién | Cuerpo | Respuestas |
|---|---|---|---|
| `GET /api/cafeterias` | Cualquier rol (solo activas) | — | 200 · 401 |
| `GET /api/cafeterias?incluirInactivas=true` | `AdminSistema` | — | 200 · 401 · 403 |
| `GET /api/cafeterias/{id}` | Cualquier rol; una inactiva solo su encargado y `AdminSistema` (los demás reciben 404) | — | 200 · 401 · 404 |
| `POST /api/cafeterias` | `AdminSistema` | `GuardarCafeteriaDto` | 201 (+ `Location`) · 400 · 401 · 403 · 409 |
| `PUT /api/cafeterias/{id}` | Su encargado (`AdminCafeteria` de **esa** cafetería) o `AdminSistema` | `GuardarCafeteriaDto` | 200 · 400 · 401 · 403 · 404 · 409 |
| `DELETE /api/cafeterias/{id}` | `AdminSistema` | — | 204 · 401 · 403 · 404 · 409 |
| `POST /api/cafeterias/{id}/reactivar` | `AdminSistema` | — | 204 · 401 · 403 · 404 |

```json
// POST y PUT: GuardarCafeteriaDto
{ "nombre": "Cafetería Norte", "ubicacion": "Edificio K, planta baja", "esDelCampus": true,
  "puntoEntrega": "Ventanilla 2", "metodosPago": ["Efectivo", "Tarjeta"] }

// Respuesta (lista, detalle, POST y PUT)
{ "idCafeteria": "…", "nombre": "Cafetería Norte", "ubicacion": "Edificio K, planta baja", "esDelCampus": true,
  "puntoEntrega": "Ventanilla 2", "activa": true, "metodosPago": ["Tarjeta", "Efectivo"],
  "creadoEn": "2026-10-20T16:00:00+00:00", "actualizadoEn": "2026-10-20T16:00:00+00:00" }
```

| Código | Cuándo |
|---|---|
| 400 | Datos inválidos: nombre de 2 a 100, ubicación (máx. 150) y punto de entrega (máx. 100) obligatorios, `esDelCampus` obligatorio, al menos un método de pago (`Tarjeta`, `Transferencia`, `Efectivo`) |
| 403 | El rol no puede hacer la acción, o es encargado de **otra** cafetería |
| 404 | No existe, o es inactiva y no la administras |
| 409 | Ya existe una cafetería con ese nombre (sin distinguir mayúsculas), o se intenta desactivar con órdenes en curso |

- Los métodos de pago se envían y se reciben como texto; un número o un método que no existe da 400.
- `PUT` reemplaza los métodos de pago por la lista enviada. No cambia `activa`: para eso están `DELETE` y `reactivar`.
- Desactivar o reactivar dos veces no es error.

## Alta de producto (TEC-09)
`POST /api/cafeterias/{idCafeteria}/productos` · `multipart/form-data`. Solo su encargado o `AdminSistema`.
La imagen es **opcional** y sigue exactamente las mismas reglas que el cambio de imagen (sección siguiente):
JPEG, PNG o WebP, máximo 5 MB, contenido real verificado, mismos tiempos y reintentos.

| Campo | Obligatorio | Regla |
|---|---|---|
| `nombre` | Sí | 2 a 100 caracteres; único en la cafetería sin distinguir mayúsculas |
| `descripcion` | No | Máx. 500; en blanco se guarda vacía (`null`) |
| `precio` | Sí | Mayor a 0, menor a 1,000,000, máx. 2 decimales. Siempre con punto: `55.50` |
| `idCategoria` | No | Una categoría de **esa** cafetería |
| `disponible` | No | `true` / `false` (por defecto `true`) |
| `archivo` | No | La imagen |

```json
// 201
{ "idProducto": "…", "idCafeteria": "…", "idCategoria": null, "categoria": null, "nombre": "Chilaquiles verdes",
  "descripcion": "Con pollo y crema", "precio": 55.50, "imagenUrl": "https://…/productos/<idProducto>_….webp",
  "disponible": true, "creadoEn": "…", "actualizadoEn": "…" }
```

Responde lo mismo que el cambio de imagen (400, 403, 404, 413, 503, 504) y además **409** si ya existe un producto
con ese nombre. Si la imagen no es válida no se crea el producto; si la BD rechaza el alta, se borra la imagen que
ya se había subido. Un alta repetida por mala señal no duplica: la segunda responde 409.

Igual que en cafeterías, `sp_crear_producto` solo guarda y la API lee el producto con `sp_obtener_producto` para
responder; el 201 trae `Location` al GET del producto.

`GET /api/cafeterias/{idCafeteria}/productos/{idProducto}` · cualquier usuario con sesión. Devuelve el mismo objeto
que el alta. Un producto no disponible, o de una cafetería inactiva, solo lo ven su encargado y `AdminSistema`; para
los demás es **404**, igual que si no existiera o fuera de otra cafetería.

## Imagen de producto (TEC-10)
`POST /api/cafeterias/{idCafeteria}/productos/{idProducto}/imagen` · `multipart/form-data` con el campo `archivo`.
Solo su encargado (`AdminCafeteria` de **esa** cafetería) o `AdminSistema`. La imagen se guarda en Azure Blob Storage
(contenedor `productos`, lectura pública) y su URL en `producto.imagen_url`.

```json
// 200
{ "imagenUrl": "https://campusbite.blob.core.windows.net/productos/<idProducto>_20261027T120000123Z.webp" }
```

| Código | Cuándo | Qué hacer en el front |
|---|---|---|
| 400 | Falta `archivo`; no es JPEG, PNG o WebP; está vacío; pesa más de 5 MB; o su contenido no es de verdad ese formato | Mostrar el `detail` |
| 403 / 404 | Rol sin permiso o encargado de otra cafetería / el producto no existe o no es de esa cafetería | Mostrar el `detail` |
| 413 | El cuerpo ya dice (`Content-Length`) que pesa mucho más de 5 MB: se rechaza sin recibirlo | Pedir una imagen más chica |
| 503 | Azure no respondió después de reintentar; trae `Retry-After: 5` | Esperar y **repetir la misma petición** |
| 504 | La subida tardó más de 3 minutos (señal muy débil) | Avisar y repetir cuando mejore la señal |

Pensado para celulares con mala señal:
- **Reintentar es seguro.** Si el celular pierde la respuesta y repite la subida, queda una sola imagen: cada subida
  guarda la suya y borra la anterior. Orden interno: subir la nueva → guardar la URL → borrar la vieja; si algo
  falla a la mitad, el producto sigue con su imagen anterior y no quedan archivos huérfanos.
- Kestrel ya no corta una subida lenta a los pocos segundos (mínimo 64 bytes/s con 30 s de gracia) y el tiempo total
  se limita a 3 minutos (`SubidaDeArchivoAttribute`), antes de los 230 s en que corta Azure App Service.
- Entre la API y Azure: reintentos exponenciales (4, de 1 a 10 s, 30 s por intento) y envío en bloques de 1 MB, así un
  corte solo repite el bloque que falló. Se ajustan con `AzureBlob__*` (ver `.env.example`).
- Sin `AzureBlob__ConnectionString` la API arranca igual, pero esta ruta responde 503 (queda el motivo en el log).
  En local se puede usar [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) con
  `AzureBlob__ConnectionString=UseDevelopmentStorage=true` (crea antes el contenedor `productos` con acceso público).

## API de autenticación
Base: `/api/auth`. Todas las respuestas son JSON; los errores usan el formato ProblemDetails
(`status`, `title`, `detail` y, en errores de validación, `errors` con un mensaje por campo).

| Método y ruta | Auth | Cuerpo | Respuestas |
|---|---|---|---|
| `POST /api/auth/registro` | No | `nombre`, `correo`, `password`, `matricula?`, `telefono?` | 201 · 400 · 409 · 429 |
| `POST /api/auth/registro-cafeteria` | No | `nombreEncargado`, `correo`, `password`, `telefono?`, `nombreCafeteria`, `ubicacion`, `esDelCampus`, `puntoEntrega`, `metodosPago` | 201 · 400 · 409 · 429 |
| `POST /api/auth/login` | No | `correo`, `password` | 200 · 400 · 401 · 429 |
| `GET /api/auth/me` | Bearer (cualquier rol) | — | 200 · 401 |

**Registro de negocio** (TEC-11): un dueño crea su cuenta y su cafetería en un solo paso. El usuario nace
`AdminCafeteria` y encargado de esa cafetería, y recibe su token como en `/registro`. Todo ocurre en una transacción
(`sp_autorregistrar_cafeteria`): si el correo o el nombre de la cafetería ya existen (este sin distinguir
mayúsculas), responde 409 con un mensaje distinto para cada caso y no queda nada creado a medias. Es distinto de
`POST /api/cafeterias`, donde el `AdminSistema` da de alta una cafetería sin encargado.

```json
// POST /api/auth/registro-cafeteria
{ "nombreEncargado": "Ana López", "correo": "ana@negocio.mx", "password": "Password123", "telefono": null,
  "nombreCafeteria": "Café Norte", "ubicacion": "Edificio K", "esDelCampus": true,
  "puntoEntrega": "Ventanilla 2", "metodosPago": ["Efectivo", "Tarjeta"] }
```

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

# 4) Pruebas de mutación (opcional; revisan que las pruebas de verdad detecten errores)
dotnet tool install -g dotnet-stryker                       # solo la primera vez
cd tests/CampusBite.Application.Tests && dotnet stryker      # reporte en StrykerOutput/ (ignorado por git)
```

Las pruebas de mutación (Stryker) cambian el código a propósito (un `>` por `>=`, quitan una línea...) y revisan
que alguna prueba falle. Cada proyecto de pruebas trae su `stryker-config.json` con los archivos a mutar y un
mínimo de 80 %. Al cerrar TEC-09/TEC-10: Application 95 % y Api 98 %. Lo que sobrevive son mutantes equivalentes
(valores por defecto de DTOs, liberar un `MemoryStream`), que ninguna prueba puede distinguir.

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
