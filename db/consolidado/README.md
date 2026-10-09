# Script consolidado de la base de datos (pruebas locales)

> Este archivo lo genera `build_schema.sh`. No lo edites a mano: se reescribe cada vez que se ejecuta.

**Versión actual: `V1.0.0.001`** · generada el 2026-10-09 16:12:10 -0600 por Johann Yamil · archivo
[`versiones/campusbite_V1.0.0.001.sql`](versiones/campusbite_V1.0.0.001.sql)

Un solo archivo `.sql` con todo el esquema de CampusBite, el login de desarrollo de `app_backend` y los datos
de prueba, para levantar la base en local **sin Docker ni Liquibase**. Producción no usa este archivo: se migra
con Liquibase desde `db/changelog` (ver `db/README.md`).

## Comandos rápidos

Desde la **raíz del repo**, en **Git Bash** (Windows) o en la terminal (Linux/macOS):

| Para qué | Comando |
|---|---|
| **Generar una versión nueva del script** | `./db/consolidado/build_schema.sh` |
| Forzar versión nueva aunque no cambie nada | `./db/consolidado/build_schema.sh --force` |
| Crear tu base local vacía | `createdb -U postgres campusbite` |
| Cargar la versión actual en tu base local | `psql -U postgres -d campusbite -f db/consolidado/versiones/campusbite_V1.0.0.001.sql` |
| Ver qué versión tiene tu base | `psql -U postgres -d campusbite -c "SELECT obj_description('public'::regnamespace);"` |
| Correr las 16 verificaciones de Joseph | `psql -U postgres -d campusbite -f db/tests/smoke_test.sql` |
| Borrar tu base local para empezar de cero | `dropdb -U postgres campusbite` |

> En PowerShell o CMD no corre `./build_schema.sh`: usa Git Bash, o `bash db/consolidado/build_schema.sh`.

## Probar en local

Requisito: PostgreSQL 14 o superior con `psql` en el PATH.

```bash
createdb -U postgres campusbite
psql -U postgres -d campusbite -f db/consolidado/versiones/campusbite_V1.0.0.001.sql
```

En el `.env` de la raíz del repo, la conexión local ya viene así en `.env.example`:

```
ConnectionStrings__Local=Host=localhost;Port=5432;Database=campusbite;Username=app_backend;Password=app_backend_dev
```

- El script corre en **una sola transacción**: si algo falla, la base queda como estaba.
- Si la base **ya tiene** el esquema, se detiene sin cambiar nada. Para cargar una versión nueva:
  `dropdb -U postgres campusbite`, `createdb -U postgres campusbite` y vuelve a cargar el script.
- Incluye la clave de desarrollo `app_backend_dev` y datos de prueba: **nunca** lo corras en un servidor real.

## Generar una versión nueva (cuando cambia la base)

1. Crea la migración en `db/changelog/migrations/` y su changeSet en `changelog-master.xml` (flujo de Liquibase).
2. Agrega la ruta del archivo en `orden.txt`, en el lugar donde debe ejecutarse.
3. Genera la versión:
   ```bash
   ./db/consolidado/build_schema.sh
   ```
   Salida esperada: `Versión V1.0.0.NNN generada` con la ruta del archivo nuevo en `versiones/`.
4. Sube a Git los scripts nuevos, `orden.txt`, `historial.txt`, este `README.md` y el archivo nuevo de `versiones/`.

Reglas del consolidador:
- La versión es `V1.0.0.NNN`: el último número sube **1** en cada versión.
- Solo genera versión si cambió el contenido de algún script (compara la huella); si no, responde
  "Sin cambios..." y solo actualiza este README. Para forzarla: `--force`.
- Se detiene si `orden.txt` menciona un archivo que no existe, o si falta alguna migración que sí está en
  `changelog-master.xml`.
- No edites migraciones ya aplicadas: Liquibase las protege por checksum. Los cambios van en una migración nueva.

## Historial de versiones

| Versión | Fecha y hora | Huella | Scripts | Autor |
|---|---|---|---|---|
| `V1.0.0.001` | 2026-10-09 16:12:10 -0600 | `3b3a2842e3aa` | V001__esquema_inicial, V002__logica_negocio, V003__vistas_y_seguridad, V004__endurecimiento_seguridad, V005__depuracion_indices, 01_login_app_backend_dev, seed_dev | Johann Yamil |
