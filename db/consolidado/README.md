# Script consolidado de la base de datos (pruebas locales)

> Este archivo lo genera `build_schema.sh`. No lo edites a mano: tus cambios se perderán en la próxima versión.

**Versión actual: `V1.0.0.001`** · generada el 2026-10-09 16:12:10 -0600 por Johann Yamil · archivo
[`versiones/campusbite_V1.0.0.001.sql`](versiones/campusbite_V1.0.0.001.sql)

Un solo archivo `.sql` con todo el esquema de CampusBite, el login de desarrollo de `app_backend` y los datos
de prueba, para levantar la base en local **sin Docker ni Liquibase**. Producción no usa este archivo: se migra
con Liquibase desde `db/changelog` (ver `db/README.md`).

## Probar en local

Requisito: PostgreSQL 14 o superior con `psql` en el PATH.

```bash
createdb -U postgres campusbite
psql -U postgres -d campusbite -f db/consolidado/versiones/campusbite_V1.0.0.001.sql
```

Luego, en el `.env` de la raíz del repo:

```
ConnectionStrings__Database=Host=localhost;Port=5432;Database=campusbite;Username=app_backend;Password=app_backend_dev
```

Verificaciones opcionales:

```bash
psql -U postgres -d campusbite -c "SELECT obj_description('public'::regnamespace);"   # muestra la versión aplicada
psql -U postgres -d campusbite -f db/tests/smoke_test.sql                             # 16 verificaciones de Joseph
```

- El script corre en **una sola transacción**: si algo falla, la base queda como estaba.
- Si la base **ya tiene** el esquema, se detiene sin cambiar nada. Para empezar de cero:
  `dropdb -U postgres campusbite` y repite los pasos.
- Incluye la clave de desarrollo `app_backend_dev` y datos de prueba: **nunca** lo corras en un servidor real.

## Generar una versión nueva

1. Crea la migración en `db/changelog/migrations/` y su changeSet en `changelog-master.xml` (flujo de Liquibase).
2. Agrega la ruta del archivo en `orden.txt`, en el lugar donde debe ejecutarse.
3. Ejecuta desde la raíz del repo (Git Bash en Windows):
   ```bash
   ./db/consolidado/build_schema.sh
   ```
4. Sube a Git los scripts nuevos, `orden.txt`, `historial.txt`, este `README.md` y el archivo nuevo de `versiones/`.

Reglas del consolidador:
- La versión es `V1.0.0.NNN`: el último número sube **1** en cada versión.
- Solo genera versión si cambió el contenido de algún script (compara la huella). Para forzarla: `--force`.
- Se detiene si `orden.txt` menciona un archivo que no existe, o si falta alguna migración que sí está en
  `changelog-master.xml`.
- No edites migraciones ya aplicadas: Liquibase las protege por checksum. Los cambios van en una migración nueva.

## Historial de versiones

| Versión | Fecha y hora | Huella | Scripts | Autor |
|---|---|---|---|---|
| `V1.0.0.001` | 2026-10-09 16:12:10 -0600 | `3b3a2842e3aa` | V001__esquema_inicial, V002__logica_negocio, V003__vistas_y_seguridad, V004__endurecimiento_seguridad, V005__depuracion_indices, 01_login_app_backend_dev, seed_dev | Johann Yamil |
