# Script consolidado de la base de datos

> Este archivo lo genera `build_schema.sh`. No lo edites a mano: se reescribe cada vez que se ejecuta.

**Versión actual: `V1.0.0.004`** · generada el 2026-10-10 10:04:26 -0600 por Joseph Yañez

| Archivo | Para qué |
|---|---|
| [`versiones/campusbite_V1.0.0.004.sql`](versiones/campusbite_V1.0.0.004.sql) | Local: esquema + clave de desarrollo de `app_backend` + datos de prueba |
| [`versiones/campusbite_V1.0.0.004_prod.sql`](versiones/campusbite_V1.0.0.004_prod.sql) | Producción (Neon): solo esquema, sin datos de prueba ni claves |

Los dos son **idempotentes**: se pueden ejecutar sobre una base vacía o sobre una que ya tenga una versión
anterior (incluida V1.0.0.001). Cada script revisa antes de crear o cambiar algo, así que repetirlo no
falla ni duplica nada, y los datos existentes se conservan. Todo corre en una transacción: si algo falla,
la base queda como estaba.


Desde la **raíz del repo**, en **Git Bash** (Windows) o en la terminal (Linux/macOS):

| Para qué | Comando |
|---|---|
| **Generar una versión nueva del script** | `./db/consolidado/build_schema.sh` |
| Forzar versión nueva aunque no cambie nada | `./db/consolidado/build_schema.sh --force` |
| Crear tu base local vacía | `createdb -U postgres campusbite` |
| Cargar o actualizar tu base local | `psql -U postgres -d campusbite -f db/consolidado/versiones/campusbite_V1.0.0.004.sql` |
| Actualizar producción (Neon) | `psql "<cadena de neondb_owner>" -f db/consolidado/versiones/campusbite_V1.0.0.004_prod.sql` |
| Ver qué versión tiene una base | `psql -U postgres -d campusbite -c "SELECT obj_description('public'::regnamespace);"` |
| Correr las pruebas de humo | `psql -U postgres -d campusbite -f db/tests/smoke_test.sql` |
| Borrar tu base local para empezar de cero | `dropdb -U postgres campusbite` |

> En PowerShell o CMD no corre `./build_schema.sh`: usa Git Bash, o `bash db/consolidado/build_schema.sh`.

Requisito: PostgreSQL 14 o superior (usa `CREATE OR REPLACE TRIGGER` y procedimientos con `OUT`).

En el `.env` de la raíz del repo, la conexión local ya viene así en `.env.example`:

```
ConnectionStrings__Local=Host=localhost;Port=5432;Database=campusbite;Username=app_backend;Password=app_backend_dev
```

- El archivo local incluye la clave de desarrollo `app_backend_dev` y datos de prueba: **nunca** lo corras en un servidor real.
- El archivo `_prod` no toca la clave de `app_backend`: se asigna a mano (ver `db/README.md`).

## Generar la versión del script (manual, cuando se ocupe)

El script **no se ejecuta solo**: genera una versión únicamente cuando alguien corre estos comandos.
En **Git Bash** (Windows) o en la terminal (Linux/macOS):

```bash
cd db/consolidado
chmod +x build_schema.sh      # solo la primera vez (da permiso de ejecución)
./build_schema.sh             # genera V1.0.0.NNN si cambió algún script; si no, responde "Sin cambios"
```

No lleva nombre de módulo: arma toda la base siguiendo `orden.txt` (módulos) y el `orden.txt` de cada módulo.
Si cambió algo, aparecen los dos archivos nuevos en `versiones/` y se actualizan `historial.txt`, este README y
`db/changelog/changelog-master.xml`.

## Cómo se organiza

```
db/
  consolidado/orden.txt        módulos en el orden en que se ejecutan
  scripts/<módulo>/orden.txt   scripts del módulo en el orden en que se ejecutan
  scripts/<módulo>/NNN_*.sql   un cambio por archivo, idempotente
```

Directivas en el `orden.txt` de un módulo (líneas de comentario):
- `# liquibase: context=dev`: el módulo es solo de desarrollo. Queda fuera del archivo `_prod` y Liquibase lo
  aplica solo con el contexto `dev`.
- `# liquibase: runAlways`: Liquibase lo ejecuta en cada `update` (se usa en los permisos).

## Generar una versión nueva (cuando cambia la base)

1. Crea el script en el módulo que corresponda, por ejemplo `db/scripts/03_cambios_sprint2/006_agregar_columna_x.sql`,
   usando `IF NOT EXISTS`, `CREATE OR REPLACE` o un bloque `DO $$` que revise antes de cambiar.
2. Agrega el nombre del archivo al final del `orden.txt` de ese módulo. Si es un módulo nuevo, agrega su carpeta
   en `db/consolidado/orden.txt`.
3. Genera la versión:
   ```bash
   ./db/consolidado/build_schema.sh
   ```
   Salida esperada: `Versión V1.0.0.NNN generada` con las rutas de los archivos nuevos.
4. Sube a Git los scripts nuevos, los `orden.txt`, `historial.txt`, este `README.md`, `changelog-master.xml`
   y los dos archivos nuevos de `versiones/`.

Reglas del consolidador:
- La versión es `V1.0.0.NNN`: el último número sube **1** en cada versión.
- Solo genera versión si cambió el contenido o el orden de algún script (compara la huella); si no, responde
  "Sin cambios..." y solo actualiza este README. Para forzarla: `--force`.
- Se detiene si un `orden.txt` menciona un archivo o carpeta que no existe, o si un script de una carpeta de módulo
  no está en su `orden.txt` (para que no se quede nada fuera por olvido).
- Las tablas (`02_tablas`) no se editan una vez publicadas: un cambio de columna va en un script nuevo para que
  las bases existentes también lo reciban. Triggers, vistas, procedimientos y permisos sí se editan en su archivo.

## Historial de versiones

| Versión | Fecha y hora | Huella | Scripts | Autor |
|---|---|---|---|---|
| `V1.0.0.004` | 2026-10-10 10:04:26 -0600 | `be3b0e1ef0cf` | 01_base/001_extensiones, 01_base/002_tipos, 01_base/003_rol_app_backend, 02_tablas/001_rol, 02_tablas/002_usuario, 02_tablas/003_cafeteria, 02_tablas/004_horario_cafeteria, 02_tablas/005_categoria, 02_tablas/006_producto, 02_tablas/007_orden, 02_tablas/008_detalle_orden, 02_tablas/009_pago, 02_tablas/010_historial_estado_orden, 02_tablas/011_suscripcion_push, 02_tablas/012_indices, 02_tablas/013_bitacora, 03_cambios_sprint2/000_retirar_objetos_v1, 03_cambios_sprint2/001_depurar_indices, 03_cambios_sprint2/002_metodos_pago_cafeteria, 03_cambios_sprint2/003_personal_cafeteria, 03_cambios_sprint2/004_quitar_total_orden, 03_cambios_sprint2/005_fechas, 03_cambios_sprint2/006_fechas_en_todas_las_tablas, 04_triggers/001_actualizado_en, 04_triggers/002_maquina_estados_orden, 04_triggers/003_historial_orden, 04_triggers/004_cancelar_pago, 04_triggers/005_validar_detalle, 04_triggers/006_cafeteria_inmutable, 04_triggers/007_validar_monto_pago, 04_triggers/008_bitacora, 05_vistas/001_vw_menu_cafeteria, 05_vistas/002_vw_metodos_pago_cafeteria, 05_vistas/003_vw_cola_cafeteria, 05_vistas/004_vw_mis_ordenes, 06_procedimientos/001_sp_exigir_gestor_cafeteria, 06_procedimientos/002_sp_registrar_usuario, 06_procedimientos/003_sp_obtener_credenciales, 06_procedimientos/004_sp_menu_cafeteria, 06_procedimientos/005_sp_crear_orden, 06_procedimientos/006_sp_confirmar_pago, 06_procedimientos/007_sp_cambiar_estado_orden, 06_procedimientos/008_sp_cancelar_orden, 06_procedimientos/009_sp_entregar_orden, 06_procedimientos/010_sp_mis_ordenes, 06_procedimientos/011_sp_cola_cafeteria, 06_procedimientos/012_sp_bitacora_registro, 07_seguridad/001_privilegios, 07_seguridad/002_limites_rol_api, 07_seguridad/003_rls, 90_datos_dev/001_login_app_backend, 90_datos_dev/002_datos_prueba | Joseph Yañez |
| `V1.0.0.003` | 2026-10-10 09:57:16 -0600 | `2fff03b9458e` | 01_base/001_extensiones, 01_base/002_tipos, 01_base/003_rol_app_backend, 02_tablas/001_rol, 02_tablas/002_usuario, 02_tablas/003_cafeteria, 02_tablas/004_horario_cafeteria, 02_tablas/005_categoria, 02_tablas/006_producto, 02_tablas/007_orden, 02_tablas/008_detalle_orden, 02_tablas/009_pago, 02_tablas/010_historial_estado_orden, 02_tablas/011_suscripcion_push, 02_tablas/012_indices, 03_cambios_sprint2/000_retirar_objetos_v1, 03_cambios_sprint2/001_depurar_indices, 03_cambios_sprint2/002_metodos_pago_cafeteria, 03_cambios_sprint2/003_personal_cafeteria, 03_cambios_sprint2/004_quitar_total_orden, 03_cambios_sprint2/005_fechas, 04_triggers/001_actualizado_en, 04_triggers/002_maquina_estados_orden, 04_triggers/003_historial_orden, 04_triggers/004_cancelar_pago, 04_triggers/005_validar_detalle, 04_triggers/006_cafeteria_inmutable, 04_triggers/007_validar_monto_pago, 05_vistas/001_vw_menu_cafeteria, 05_vistas/002_vw_metodos_pago_cafeteria, 05_vistas/003_vw_cola_cafeteria, 05_vistas/004_vw_mis_ordenes, 06_procedimientos/001_sp_exigir_gestor_cafeteria, 06_procedimientos/002_sp_registrar_usuario, 06_procedimientos/003_sp_obtener_credenciales, 06_procedimientos/004_sp_menu_cafeteria, 06_procedimientos/005_sp_crear_orden, 06_procedimientos/006_sp_confirmar_pago, 06_procedimientos/007_sp_cambiar_estado_orden, 06_procedimientos/008_sp_cancelar_orden, 06_procedimientos/009_sp_entregar_orden, 06_procedimientos/010_sp_mis_ordenes, 06_procedimientos/011_sp_cola_cafeteria, 07_seguridad/001_privilegios, 07_seguridad/002_limites_rol_api, 07_seguridad/003_rls, 90_datos_dev/001_login_app_backend, 90_datos_dev/002_datos_prueba | Joseph Yañez |
| `V1.0.0.002` | 2026-10-10 09:51:31 -0600 | `49b85978b645` | 01_base/001_extensiones, 01_base/002_tipos, 01_base/003_rol_app_backend, 02_tablas/001_rol, 02_tablas/002_usuario, 02_tablas/003_cafeteria, 02_tablas/004_horario_cafeteria, 02_tablas/005_categoria, 02_tablas/006_producto, 02_tablas/007_orden, 02_tablas/008_detalle_orden, 02_tablas/009_pago, 02_tablas/010_historial_estado_orden, 02_tablas/011_suscripcion_push, 02_tablas/012_indices, 03_cambios_sprint2/000_retirar_objetos_v1, 03_cambios_sprint2/001_depurar_indices, 03_cambios_sprint2/002_metodos_pago_cafeteria, 03_cambios_sprint2/003_personal_cafeteria, 03_cambios_sprint2/004_quitar_total_orden, 03_cambios_sprint2/005_fechas, 04_triggers/001_actualizado_en, 04_triggers/002_maquina_estados_orden, 04_triggers/003_historial_orden, 04_triggers/004_cancelar_pago, 04_triggers/005_validar_detalle, 04_triggers/006_cafeteria_inmutable, 04_triggers/007_validar_monto_pago, 05_vistas/001_vw_menu_cafeteria, 05_vistas/002_vw_metodos_pago_cafeteria, 05_vistas/003_vw_cola_cafeteria, 05_vistas/004_vw_mis_ordenes, 06_procedimientos/001_sp_exigir_gestor_cafeteria, 06_procedimientos/002_sp_registrar_usuario, 06_procedimientos/003_sp_obtener_credenciales, 06_procedimientos/004_sp_menu_cafeteria, 06_procedimientos/005_sp_crear_orden, 06_procedimientos/006_sp_confirmar_pago, 06_procedimientos/007_sp_cambiar_estado_orden, 06_procedimientos/008_sp_cancelar_orden, 06_procedimientos/009_sp_entregar_orden, 06_procedimientos/010_sp_mis_ordenes, 06_procedimientos/011_sp_cola_cafeteria, 07_seguridad/001_privilegios, 07_seguridad/002_limites_rol_api, 07_seguridad/003_rls, 90_datos_dev/001_login_app_backend, 90_datos_dev/002_datos_prueba | Joseph Yañez |
| `V1.0.0.001` | 2026-10-09 16:12:10 -0600 | `3b3a2842e3aa` | V001__esquema_inicial, V002__logica_negocio, V003__vistas_y_seguridad, V004__endurecimiento_seguridad, V005__depuracion_indices, 01_login_app_backend_dev, seed_dev | Johann Yamil |
