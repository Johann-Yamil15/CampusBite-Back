# CampusBite · CAMPUS-11 (TEC-03) · Base de datos en contenedor + Liquibase

PostgreSQL 16 en Docker, esquema versionado con **Liquibase**. El modelo viene de TEC-02.

```
docker-compose.yml             PostgreSQL 16 (volumen persistente) + Liquibase
.env.example                   Variables de conexión (copiar a .env; no se sube a Git)
00_crear_base_datos.sql        Solo para PostgreSQL instalado a mano (con Docker no hace falta)
changelog/
  changelog-master.xml         Changesets V001–V005 (+ login y seed de dev) con su rollback
  migrations/V001–V005.sql     Esquema, lógica (triggers, sp_*), vistas, seguridad, índices
  migrations/down/             SQL de rollback de cada migración
  seeds/seed_dev.sql           Datos de prueba (solo contexto "dev")
tests/smoke_test.sql           Flujo completo + seguridad + índices, 16 verificaciones; termina en ROLLBACK
```

## Uso local con Docker

```bash
cd db
cp .env.example .env                      # PowerShell: Copy-Item .env.example .env  (y cambia DB_PASSWORD)
docker compose up -d db                   # levanta PostgreSQL y espera a que esté listo
docker compose run --rm liquibase         # aplica las migraciones pendientes (update)
docker compose exec -T db psql -U postgres -d campusbite -v ON_ERROR_STOP=1 < tests/smoke_test.sql
```

Otros comandos (mismo servicio, cambia el último argumento: `status`, `history`, `validate`, `rollback-count 1`):

```bash
docker compose run --rm liquibase --search-path=/liquibase/changelog --changelog-file=changelog-master.xml \
  --url=jdbc:postgresql://db:5432/campusbite --username=postgres --password=$DB_PASSWORD status
```

Borrar todo (contenedor y datos): `docker compose down -v`.

Contextos: `dev` (esquema + datos de prueba + login de `app_backend`) y `prod` (solo esquema). Nunca uses `dev` en un servidor real.

## Seguridad

**Modelo de confianza.** El backend autentica a la persona (JWT, sesión) y le pasa su `id` a la base. La base no
expone tablas a la API: solo procedimientos `SECURITY DEFINER` con `search_path` fijo y consultas que **exigen** el
filtro por dueño. Si el backend se equivoca, la base no devuelve datos ajenos.

| Capa | Qué hace |
|---|---|
| Rol `app_backend` | Único rol de la API. Sin acceso a tablas; ejecuta `sp_*` y lee `vw_menu_cafeteria`. Límite de 50 conexiones, `statement_timeout` 10 s, `lock_timeout` 3 s, `idle_in_transaction_session_timeout` 15 s |
| `PUBLIC` | Sin permisos: ni conectar, ni ejecutar funciones, ni crear objetos en `public` (V003/V004) |
| Lecturas por persona | `vw_mis_ordenes` y `vw_cola_cafeteria` ya **no** se pueden leer directo. La API usa `sp_mis_ordenes(id_usuario, limite, desplazamiento)` (máx. 100 filas) y `sp_cola_cafeteria(id_cafeteria, id_actor)` (verifica que sea el encargado o admin) |
| Escrituras | `sp_registrar_usuario`, `sp_crear_orden`, `sp_confirmar_pago`, `sp_cambiar_estado_orden`, `sp_cancelar_orden`, `sp_entregar_orden`: validan permisos, estados y montos dentro de la transacción |
| Integridad | Triggers: máquina de estados de la orden, historial automático, reembolso al cancelar, validación de detalle |
| Código de recogida | Generado con `gen_random_uuid()` (aleatorio criptográfico), no con `random()` |
| Contraseñas | Solo se guarda el hash que genera el backend (bcrypt/argon2); la base nunca ve la clave en claro |

**Qué usar desde la API (.NET):** Npgsql con el usuario `app_backend`, llamando funciones, p. ej.
`SELECT * FROM sp_mis_ordenes(@id, 20, 0)`. Cadena de conexión de desarrollo:
`Host=localhost;Database=campusbite;Username=app_backend;Password=app_backend_dev`.

**Lista de verificación para producción**
1. `ALTER ROLE app_backend WITH LOGIN PASSWORD '<clave larga>'` a mano o desde el gestor de secretos; nunca en un script del repo.
2. Exigir SSL (Azure: `require_secure_transport = ON`; AWS RDS: `rds.force_ssl = 1`) y `sslmode=require` en la cadena.
3. Ejecutar Liquibase con un usuario **migrador** distinto de `app_backend` y guardar sus credenciales fuera del repo.
4. Restringir el firewall / grupo de seguridad a la IP o red de la API; sin acceso público abierto.
5. Respaldos automáticos con cifrado y retención; probar una restauración.
6. Activar auditoría del servicio (Azure: `pgaudit`; AWS: `pgaudit` en el grupo de parámetros) si el curso o la normativa lo piden.
7. Rotar la clave de `app_backend` periódicamente (el backend lee la clave de variables de entorno o *user secrets*).

## Indexación (V005): medida, no supuesta

Se cargaron 300 mil órdenes, 600 mil detalles y 890 mil cambios de estado, se corrió la carga de trabajo real de la
API con `pgbench` y se revisó `pg_stat_user_indexes`.

| Índice | Decisión | Evidencia |
|---|---|---|
| `idx_orden_usuario_fecha` | Se queda | "Mis órdenes": cientos de miles de usos, 0.08 ms |
| `idx_orden_cola_cafeteria` (parcial) | Se queda | Cola de la cafetería; solo indexa órdenes activas (96 kB) |
| `idx_producto_cafeteria` (parcial) | Se queda | Menú: >100 mil usos |
| `UNIQUE` de `detalle_orden`, `pago.id_orden`, `usuario.correo` | Se quedan | Respaldan restricciones y las consultas más frecuentes (login, joins) |
| `idx_historial_orden_fecha` | Se queda | Línea de tiempo de una orden: 0.6 ms entre 890 mil filas |
| `idx_detalle_producto`, `idx_push_usuario` | Se quedan | FK de la tabla más grande / envío de notificaciones |
| `idx_horario_cafeteria_dia` | **Se elimina** | Redundante con el `UNIQUE (id_cafeteria, dia_semana, hora_apertura)` |
| `idx_usuario_rol` | **Se elimina** | La columna tiene 3 valores: nunca es selectivo (0 usos) |
| `idx_cafeteria_activa`, `idx_cafeteria_admin`, `idx_producto_categoria` | **Se eliminan** | Tablas de decenas de filas, 0–12 usos: el barrido secuencial es más rápido |

Los índices sobrantes en tablas pequeñas no cambian el rendimiento; se quitaron para no cargar escrituras ni mantenimiento.
Lo que importa está en `orden`, `detalle_orden` e `historial_estado_orden`, y ahí el diseño ya era adecuado.

**Candidatos: se agregan solo si existe la funcionalidad** (mediciones con 300 mil órdenes):

```sql
-- Reporte de ventas por cafetería y fecha: 39.6 ms -> 0.5 ms (12 MB)
CREATE INDEX idx_orden_cafeteria_fecha ON orden (id_cafeteria, creada_en);
-- Cancelar órdenes pendientes de pago vencidas (tarea programada): 35.7 ms -> ~0 ms (casi sin tamaño)
CREATE INDEX idx_orden_pendientes ON orden (creada_en) WHERE estado = 'pendiente';
```

Cada índice nuevo encarece las escrituras de `orden`; por eso no se crean "por si acaso". Van como una migración nueva (V006).

Nota de escala: las llaves `UUID v4` aleatorias fragmentan los índices de las tablas con más inserciones
(`detalle_orden_pkey` 24 MB y `historial_estado_orden_pkey` 35 MB, sin lecturas). Para un campus no es un problema;
si el volumen crece, `UUIDv7` (PostgreSQL 18) o una llave compuesta `(id_orden, id_producto)` en el detalle lo resuelven.

## Reglas

- No edites un changeSet ya aplicado (Liquibase detecta el cambio por checksum y se detiene). Los cambios van en un changeSet nuevo (V006, ...) con su rollback.
- Las funciones nuevas necesitan su propio `GRANT EXECUTE ... TO app_backend` (V003/V004 quitan el permiso por defecto a PUBLIC).
- Las migraciones de EF Core no se usan: el esquema lo gestiona solo Liquibase.

## Azure Database for PostgreSQL (Flexible Server)

1. Crear el servidor y la base `campusbite`.
2. **Habilitar la extensión**: en *Parámetros del servidor* agregar `CITEXT` a `azure.extensions`; sin esto V001 falla en `CREATE EXTENSION`.
3. Agregar tu IP (o la del servicio de la API) en *Redes → reglas de firewall*. Azure exige SSL.
4. Correr Liquibase contra Azure con contexto `prod`:
   ```bash
   liquibase --search-path=changelog --changelog-file=changelog-master.xml \
     --url="jdbc:postgresql://<servidor>.postgres.database.azure.com:5432/campusbite?sslmode=require" \
     --username=<admin> --password=<clave> --contexts=prod update
   ```
5. Asignar clave a `app_backend` (punto 1 de la lista de producción) y usar la cadena Npgsql con `SSL Mode=Require` y el usuario `app_backend`.

## AWS RDS para PostgreSQL (si se cambia de nube)

No hace falta cambiar el SQL: `citext` se habilita con `CREATE EXTENSION` desde el usuario administrador, sin lista de permitidos. Cambia
solo la URL de Liquibase (`jdbc:postgresql://<instancia>.rds.amazonaws.com:5432/campusbite?sslmode=require`), el grupo de
seguridad en lugar del firewall y `rds.force_ssl = 1`. Los `ALTER ROLE ... SET` de V004 funcionan igual.
