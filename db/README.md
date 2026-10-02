# CampusBite · TEC-03 · Scripts de BD y migración inicial

PostgreSQL 14+ (probado en 16). Parte del script de TEC-02 (`campusbite_TEC-02.sql`)
dividido en migraciones versionadas, con rollback, datos de prueba y smoke test.

```
00_crear_base_datos.sql        Crea la BD `campusbite` y el rol `app_backend` (una vez, como superusuario)
migrate.sh                     Ejecutor: up | down | status (bash + psql)
migrations/
  V001__esquema_inicial.sql    Extensión citext, ENUMs, 11 tablas, índices, catálogo `rol`
  V002__logica_negocio.sql     Triggers y 7 procedimientos almacenados (sp_*)
  V003__vistas_y_seguridad.sql 3 vistas y permisos mínimos para app_backend
  down/                        Rollback de cada migración (V003 → V002 → V001)
seeds/seed_dev.sql             Datos de prueba (solo desarrollo, idempotente)
tests/smoke_test.sql           Flujo completo de órdenes con 11 verificaciones; termina en ROLLBACK
```

## Uso

```bash
# 1) Crear la base y el rol (solo local; en Supabase/Neon la BD ya existe)
psql -U postgres -f 00_crear_base_datos.sql

# 2) Migrar
export DATABASE_URL="postgresql://postgres:clave@localhost:5432/campusbite"
./migrate.sh up          # aplica pendientes (cada archivo en una transacción)
./migrate.sh status      # aplicadas / PENDIENTE

# 3) Datos de prueba y verificación (opcional)
psql "$DATABASE_URL" -f seeds/seed_dev.sql
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f tests/smoke_test.sql

# Revertir la última migración (V001 borra todos los datos)
./migrate.sh down
```

## Reglas del flujo de migraciones

- **Nunca edites una migración ya aplicada**: `migrate.sh` guarda un checksum y se detiene si cambió.
  Los cambios al esquema van en una migración nueva (`V004__descripcion.sql` + su `down/`).
- Cada migración nueva que cree funciones debe incluir su `GRANT EXECUTE ... TO app_backend`
  (V003 deja las funciones futuras sin permiso para PUBLIC).
- `app_backend` se crea `NOLOGIN`; en un ambiente real: `ALTER ROLE app_backend LOGIN PASSWORD '...'`.
- Los hashes del seed son marcadores inválidos a propósito.

## Cambios respecto a TEC-02

Solo uno: V003 añade `ALTER DEFAULT PRIVILEGES ... REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC`.
El resto del SQL es idéntico al original, solo reorganizado en archivos.

## Resultado de las pruebas (PostgreSQL 16.15)

- `up` desde cero: V001, V002, V003 aplicadas; `up` repetido: 0 aplicadas.
- `down` ×3 deja la BD vacía; `up` de nuevo la reconstruye y el smoke test pasa.
- Editar V001 ya aplicada → `migrate.sh` aborta con error.
- Seed ejecutado dos veces sin errores.
- Smoke test: 11/11 verificaciones OK.
