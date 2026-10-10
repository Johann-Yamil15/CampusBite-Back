# CampusBite · Base de datos (PostgreSQL)

El esquema vive en **scripts idempotentes organizados por módulo**. De ellos salen dos cosas:

- El **script consolidado y versionado** (`V1.0.0.NNN`), que se usa en producción (Neon) y en local sin Docker.
- El **changelog de Liquibase**, que se usa solo en local con Docker.

Las dos salen del mismo `orden.txt`, así que no pueden diferir.

```
db/
  scripts/
    01_base/                 extensión citext, tipos ENUM, rol app_backend
    02_tablas/               las 11 tablas como nacieron en V1.0.0.001 (CREATE TABLE IF NOT EXISTS), índices y bitacora
    03_cambios_sprint2/      evolución sin perder datos: métodos de pago, encargado, sin orden.total, fechas
    04_triggers/             reglas automáticas (fn_*): estados, historial, reembolso, validaciones
    05_vistas/               consultas reutilizables (vw_*), solo para los procedimientos
    06_procedimientos/       lo que llama la API: 12 PROCEDURE (sp_*)
    07_seguridad/            permisos mínimos, límites del rol de la API, RLS
    90_datos_dev/            SOLO local: clave pública de app_backend y datos de prueba
    <módulo>/orden.txt       orden de ejecución de los scripts del módulo
  consolidado/
    orden.txt                orden de los módulos
    build_schema.sh          genera la versión, el changelog y el README del consolidado
    versiones/               campusbite_V1.0.0.NNN.sql (local) y campusbite_V1.0.0.NNN_prod.sql (producción)
    historial.txt            una línea por versión (lo escribe build_schema.sh)
  changelog/changelog-master.xml   generado por build_schema.sh (no editar)
  tests/smoke_test.sql       20 verificaciones; termina en ROLLBACK
  docker-compose.yml         PostgreSQL 16 + Liquibase (local)
  00_crear_base_datos.sql    solo para PostgreSQL instalado a mano
```

## Cómo se usa

| Dónde | Cómo se crea o actualiza el esquema |
|---|---|
| Local con Docker | `docker compose up -d db` y `docker compose run --rm liquibase` (contexto `dev`) |
| Local sin Docker | `psql -U postgres -d campusbite -f consolidado/versiones/campusbite_V1.0.0.NNN.sql` |
| Producción (Neon) | `psql "<cadena de neondb_owner>" -f consolidado/versiones/campusbite_V1.0.0.NNN_prod.sql` |

Comandos completos y el historial de versiones: [`consolidado/README.md`](consolidado/README.md).

Los scripts son **idempotentes**: cada uno revisa antes de crear o cambiar algo (`IF NOT EXISTS`,
`CREATE OR REPLACE` o un bloque `DO $$` que consulta el catálogo). Por eso el mismo archivo sirve para una base
vacía y para una base con una versión anterior, y repetirlo no falla ni duplica nada. Probado en PostgreSQL 16 y 18:
base vacía, repetición, y actualización de una base V1.0.0.001 con órdenes (los datos se conservan).

### Local con Docker + Liquibase

```bash
cd db
cp .env.example .env                      # PowerShell: Copy-Item .env.example .env  (y cambia DB_PASSWORD)
docker compose up -d db
docker compose run --rm liquibase         # aplica los scripts nuevos o modificados
docker compose exec -T db psql -U postgres -d campusbite -v ON_ERROR_STOP=1 < tests/smoke_test.sql
```

Cada script es un `changeSet` con `runOnChange`: si editas un procedimiento, Liquibase lo vuelve a ejecutar. Los
permisos (`07_seguridad`) llevan `runAlways`. Para revertir en local no hay rollback por script: `docker compose down -v`
y vuelve a aplicar. Si tu base local tiene los changesets viejos (`V001-esquema-inicial`, ...), no pasa nada: los
scripts nuevos la actualizan igual.

### Producción (Neon)

Liquibase no se usa en producción. Antes de actualizar:
1. Toma un respaldo (en Neon: crea una *branch* de la base, es instantáneo).
2. Ejecuta el archivo `_prod` con el usuario dueño de la base. Corre en una transacción: si algo falla, no cambia nada.
3. Revisa los `WARNING` de la salida: avisan si un dato del modelo anterior no se pudo migrar (ver abajo) o si
   faltó permiso para algún ajuste del rol.
4. La primera vez, asigna la clave de `app_backend` a mano: `ALTER ROLE app_backend WITH LOGIN PASSWORD '<clave larga>';`

## Procedimientos de la API

La API (rol `app_backend`) **solo** puede ejecutar estos `PROCEDURE`. No lee tablas ni vistas. Se llaman con `CALL`.
Los que devuelven algo lo hacen por parámetros `OUT`: en su lugar se manda `NULL` y `CALL` regresa una fila con los
valores. Las listas llegan como `JSONB`. Ejemplo en .NET: `src/CampusBite.Infrastructure/Auth/UsuarioRepository.cs`.

| Procedimiento | Devuelve (OUT) | Qué hace |
|---|---|---|
| `sp_registrar_usuario(nombre, correo, hash, matricula, telefono, NULL)` | `p_id_usuario` | Crea un alumno. Correo o matrícula repetidos → `23505` |
| `sp_obtener_credenciales(correo, NULL, NULL, NULL, NULL)` | `p_id_usuario, p_id_rol, p_contrasena_hash, p_activo` | Para el login. Todo `NULL` si el correo no existe |
| `sp_menu_cafeteria(id_cafeteria \| NULL, NULL)` | `p_menu` (JSON) | Cafeterías activas con `metodos_pago` y `productos` |
| `sp_crear_orden(id_usuario, id_cafeteria, recogida_en, metodo, items, notas, NULL)` | `p_id_orden` | Orden + detalle + pago. `items`: `[{"id_producto":"<uuid>","cantidad":2}]`. Valida alumno, horario, recogida ≥ 5 min, método aceptado, productos. Efectivo nace `confirmada`; tarjeta o transferencia, `pendiente` |
| `sp_confirmar_pago(id_orden, referencia)` | — | Tarjeta o transferencia: pago `pagado` y orden `confirmada` |
| `sp_cambiar_estado_orden(id_orden, nuevo, id_actor)` | — | `en_preparacion`, `lista` o `cancelada`. Solo el encargado de esa cafetería o `admin_sistema` |
| `sp_cancelar_orden(id_orden, id_usuario)` | — | El alumno cancela antes de que entre a preparación |
| `sp_entregar_orden(id_orden, codigo, id_actor)` | — | Valida el código de recogida, cobra el efectivo, marca `entregada` |
| `sp_mis_ordenes(id_usuario, limite, desplazamiento, NULL)` | `p_ordenes` (JSON) | Historial del alumno, más recientes primero, máximo 100 por página |
| `sp_cola_cafeteria(id_cafeteria, id_actor, NULL)` | `p_cola` (JSON) | Pedidos activos por hora de recogida. Solo su encargado o `admin_sistema` (`42501` si no) |
| `sp_bitacora_registro(id_actor, tabla, id_registro, limite, NULL)` | `p_historial` (JSON) | Historial completo de un registro, el más reciente primero. Solo `admin_sistema` |

`sp_exigir_gestor_cafeteria` es interno (lo usan otros procedimientos) y la API no lo puede ejecutar.

**Por qué los triggers siguen siendo `fn_*`:** PostgreSQL solo permite que un trigger ejecute una `FUNCTION ...
RETURNS trigger`; un `PROCEDURE` no puede. Todo lo que llama la API es `PROCEDURE`.

## Revisión de redundancia (Sprint 2)

**El círculo usuario → orden → cafetería → usuario.** "Quién administra una cafetería" se guardaba dos veces:
`usuario.id_rol = 2` y `cafeteria.id_admin`, sin nada que las uniera. Podía quedar una cafetería administrada por un
alumno, o un encargado sin cafetería, y cada permiso tenía que revisar las dos cosas. Ahora la relación vive en un
solo lugar, **`usuario.id_cafeteria`**, con `CHECK (id_cafeteria IS NULL OR id_rol = 2)` en la misma fila.
`cafeteria` ya no apunta a `usuario`, así que la dependencia va en un solo sentido. De paso, una cafetería puede tener
varios encargados. Las otras dos relaciones del triángulo no son redundantes: `orden.id_usuario` es quien compra y
`orden.id_cafeteria` es dónde, y ninguna se deduce de la otra.

Al migrar una base vieja, si `id_admin` apuntaba a alguien que no es `admin_cafeteria`, ese dato no se copia y se
avisa con `WARNING`. Si un usuario administraba más de una cafetería, la migración se detiene y pide dejar una.

**El círculo orden → cafetería ← producto ← detalle → orden.** `orden.id_cafeteria` se podría deducir de sus productos.
Se queda porque la orden la necesita antes de tener renglones (horario, método de pago) y porque la cola se busca por
ella (`idx_orden_cola_cafeteria`). Para que esa copia no se desincronice, `fn_validar_detalle` revisa cada renglón, y
`fn_bloquear_cambio_cafeteria` impide cambiar después la cafetería de una orden o de un producto.

| Dato | Decisión |
|---|---|
| `orden.total` | **Se quitó**: era la suma del detalle y además se repetía en `pago.monto`. El único total guardado es `pago.monto` (lo cobrado). Un trigger diferido (`trg_pago_monto`, `trg_detalle_monto`) garantiza que sea igual a la suma del detalle |
| `cafeteria.acepta_efectivo/tarjeta/transferencia` | **Se quitó**: era un grupo repetido. Ahora es la tabla `cafeteria_metodo_pago (id_cafeteria, metodo)` |
| `orden.actualizado_en` | **Se quitó**: en una orden solo cambia el estado, y cada cambio ya tiene su fecha en `historial_estado_orden` |
| `orden.estado` junto al historial | Se queda: es el estado actual (la máquina de estados y el índice de la cola lo usan); el historial es la bitácora |
| `detalle_orden.subtotal` | Se queda: es columna generada, no puede desincronizarse |
| `ubicacion` y `punto_entrega` | Se quedan: son conceptos distintos (dónde está la cafetería y dónde se recoge) |

## Fechas

Convención: `TIMESTAMPTZ` (un instante, guardado en UTC) termina en `_en`; `TIME` (hora local) empieza con `hora_`;
`DATE` empieza con `fecha_`. La prueba 12 verifica que toda columna `*_en` sea `TIMESTAMPTZ`.

- `orden.creada_en` y `suscripcion_push.creada_en` → `creado_en`, igual que en el resto de las tablas.
- `orden.hora_recogida` → `recogida_en`: es un instante completo, no una hora.
- Reglas nuevas: `actualizado_en >= creado_en` (usuario, cafetería, producto), `recogida_en > creado_en` (orden) y
  `pagado_en <= actualizado_en` (pago).
- `horario_cafeteria` guarda horas locales del campus (`America/Mexico_City`); `sp_crear_orden` convierte la hora de
  recogida a esa zona antes de compararla. La base trabaja en UTC.
- **No hay `usuario.fecha_registro`**: sería `creado_en` guardado dos veces. El día se calcula:
  `(creado_en AT TIME ZONE 'America/Mexico_City')::date`.

### Fecha en cada tabla e historial de todo

**Toda tabla tiene `creado_en`** (cuándo se creó la fila) y, si sus filas se editan, **`actualizado_en`** (último
cambio, lo pone un trigger). Excepciones a propósito: `historial_estado_orden` y `bitacora` usan `cambiado_en`;
`orden` no tiene `actualizado_en` porque solo cambia su estado y cada cambio está en `historial_estado_orden`.
Al actualizar una base existente, `pago.creado_en` y `detalle_orden.creado_en` toman la fecha real de su orden.

`actualizado_en` solo guarda el último cambio. **El historial completo está en `bitacora`**: los triggers
`trg_<tabla>_bitacora` guardan cada alta (`I`), cambio (`U`) y baja (`D`) de las 11 tablas de negocio, con el registro
antes y después (JSON), quién lo hizo (`cambiado_por`, el mismo `app.id_actor` de los procedimientos) y cuándo.
Nunca guarda `contrasena_hash`, y un `UPDATE` que no cambia nada no se registra. Se consulta con
`CALL sp_bitacora_registro(id_admin, 'orden', '<id>', 50, NULL)`, solo `admin_sistema`.

Costo: unas 4 filas de bitácora por orden (orden, detalle, pago y cada cambio de estado). Si un día crece demasiado,
se puede archivar lo más viejo, por ejemplo con `DELETE ... WHERE cambiado_en < now() - interval '2 years'`.

## Seguridad

**Modelo de confianza.** El backend autentica a la persona (JWT) y le pasa su `id` a la base. La base no expone tablas a
la API: solo procedimientos `SECURITY DEFINER` con `search_path` fijo, que validan permisos y **exigen** el filtro por
dueño. Si el backend se equivoca, la base no devuelve datos ajenos.

| Capa | Qué hace |
|---|---|
| Rol `app_backend` | Ejecuta solo los 11 `sp_*` de la API. Sin acceso a tablas ni vistas. Límite de 50 conexiones, `statement_timeout` 10 s, `lock_timeout` 3 s, `idle_in_transaction_session_timeout` 15 s |
| `PUBLIC` | Sin permisos: ni conectar, ni ejecutar, ni crear objetos en `public` |
| Permisos por defecto | Corregido en Sprint 2: `ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ... FROM PUBLIC` (V003) no hacía nada, porque por esquema solo se pueden agregar permisos. Por eso `sp_mis_ordenes` y `sp_cola_cafeteria` (V004) quedaron ejecutables por cualquier rol. Ahora se usa la forma global y además `07_seguridad` revoca y vuelve a dar todo en cada ejecución. La prueba 16 lo verifica para cada `sp_*` |
| RLS | Activo en las 13 tablas, sin políticas: un rol que no sea el dueño no ve filas aunque reciba `SELECT` por error |
| Integridad | Triggers: máquina de estados, historial, reembolso al cancelar, producto de la misma cafetería, cafetería fija, monto = detalle |
| Código de recogida | Aleatorio criptográfico (`gen_random_uuid()`), no `random()` |
| Contraseñas | Solo se guarda el hash BCrypt que genera el backend |

**Lista para producción**
1. Clave de `app_backend` larga, asignada a mano o desde el gestor de secretos; nunca en un script del repo.
2. SSL obligatorio (`SSL Mode=Require` en la cadena).
3. Ejecutar los scripts con el usuario dueño (`neondb_owner`), distinto de `app_backend`, con credenciales fuera del repo.
4. Respaldo antes de cada actualización y prueba de restauración.

## Indexación: medida, no supuesta

Se cargaron 300 mil órdenes, 600 mil renglones y 890 mil cambios de estado, se corrió la carga de la API con `pgbench`
y se revisó `pg_stat_user_indexes`.

| Índice | Decisión | Evidencia |
|---|---|---|
| `idx_orden_usuario_fecha` | Se queda | "Mis órdenes": `sp_mis_ordenes` tarda ~1.3 ms con 300 mil órdenes (Sprint 2, con el total desde `pago.monto`) |
| `idx_orden_cola_cafeteria` (parcial) | Se queda | Cola de la cafetería; solo indexa órdenes activas. `sp_cola_cafeteria` ~10 ms con 40 activas entre 300 mil |
| `idx_producto_cafeteria` (parcial) | Se queda | Menú: >100 mil usos |
| `UNIQUE` de `detalle_orden`, `pago.id_orden`, `usuario.correo` | Se quedan | Respaldan restricciones y las consultas más frecuentes |
| `idx_historial_orden_fecha` | Se queda | Línea de tiempo de una orden: 0.6 ms entre 890 mil filas |
| `idx_detalle_producto`, `idx_push_usuario` | Se quedan | FK de la tabla más grande / envío de notificaciones |
| `idx_horario_cafeteria_dia` | **Eliminado** | Redundante con el `UNIQUE (id_cafeteria, dia_semana, hora_apertura)` |
| `idx_usuario_rol` | **Eliminado** | La columna tiene 3 valores: nunca es selectivo |
| `idx_cafeteria_activa`, `idx_cafeteria_admin`, `idx_producto_categoria` | **Eliminados** | Tablas de decenas de filas: el barrido secuencial es más rápido |

**Candidatos, solo si existe la funcionalidad:**

```sql
-- Reporte de ventas por cafetería y fecha: 39.6 ms -> 0.5 ms (12 MB)
CREATE INDEX idx_orden_cafeteria_fecha ON orden (id_cafeteria, creado_en);
-- Cancelar órdenes pendientes de pago vencidas: 35.7 ms -> ~0 ms
CREATE INDEX idx_orden_pendientes ON orden (creado_en) WHERE estado = 'pendiente';
```

## Reglas

- Un cambio nuevo es un **script nuevo** en su módulo, idempotente, agregado al `orden.txt` del módulo. Luego
  `./db/consolidado/build_schema.sh` genera la versión.
- Los archivos de `02_tablas` y `03_cambios_sprint2` no se editan una vez publicados: las bases que ya los
  ejecutaron no recibirían el cambio. Triggers, vistas, procedimientos y seguridad sí se editan en su archivo
  (son `CREATE OR REPLACE` o se recrean).
- Un procedimiento nuevo de la API se agrega al `GRANT` de `07_seguridad/001_privilegios.sql`.
- Sin `SELECT *`: las columnas siempre van explícitas.
- Las migraciones de EF Core no se usan.

## Azure Database for PostgreSQL / AWS RDS (si se cambia de proveedor)

No hace falta cambiar los scripts. En Azure hay que permitir `CITEXT` en el parámetro `azure.extensions` antes de
ejecutar; en AWS RDS basta con el usuario administrador. En los dos se usa el archivo `_prod` con `sslmode=require`.
