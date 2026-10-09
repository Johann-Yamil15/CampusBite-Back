#!/usr/bin/env bash
# =====================================================================
# JJ-Sprint2 09/10/2026: consolidador versionado de los scripts de BD de CampusBite
#
# Une en UN solo archivo .sql los scripts listados en orden.txt, con encabezado de versión
# (V1.0.0.NNN, el último número sube 1 en cada versión), fecha, hora, autor y huella (hash).
# Siempre regenera README.md con el historial de versiones.
#
# Uso (desde la raíz del repo, en Git Bash / Linux / macOS):
#   ./db/consolidado/build_schema.sh            genera una versión nueva solo si cambió algún script
#   ./db/consolidado/build_schema.sh --force    genera una versión nueva aunque no haya cambios
# =====================================================================
set -euo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"          # db/consolidado
DB_DIR="$(dirname "$DIR")"                     # db
MANIFEST="$DIR/orden.txt"
CHANGELOG="$DB_DIR/changelog/changelog-master.xml"
VERSIONES="$DIR/versiones"
HISTORIAL="$DIR/historial.txt"
README="$DIR/README.md"
PREFIJO="1.0.0"
FORZAR=false
[[ "${1:-}" == "--force" ]] && FORZAR=true

error() { echo "ERROR: $*" >&2; exit 1; }

# JJ-Sprint2 09/10/2026: README generado a partir de la última línea del historial (se llama siempre)
generar_readme() {
  local ultima version fecha autor
  ultima="$(grep -E '^V[0-9]' "$HISTORIAL" | tail -1)"
  version="$(echo "$ultima" | cut -d'|' -f1)"
  fecha="$(echo "$ultima" | cut -d'|' -f2)"
  autor="$(echo "$ultima" | cut -d'|' -f5)"
  {
    cat <<EOF
# Script consolidado de la base de datos (pruebas locales)

> Este archivo lo genera \`build_schema.sh\`. No lo edites a mano: se reescribe cada vez que se ejecuta.

**Versión actual: \`$version\`** · generada el $fecha por $autor · archivo
[\`versiones/campusbite_$version.sql\`](versiones/campusbite_$version.sql)

Un solo archivo \`.sql\` con todo el esquema de CampusBite, el login de desarrollo de \`app_backend\` y los datos
de prueba, para levantar la base en local **sin Docker ni Liquibase**. Producción no usa este archivo: se migra
con Liquibase desde \`db/changelog\` (ver \`db/README.md\`).

## Generar la versión del script (manual, cuando se ocupe)

El script **no se ejecuta solo**: genera una versión únicamente cuando alguien corre estos comandos.
En **Git Bash** (Windows) o en la terminal (Linux/macOS):

\`\`\`bash
cd db/consolidado
chmod +x build_schema.sh      # solo la primera vez (da permiso de ejecución)
./build_schema.sh             # genera V$PREFIJO.NNN si cambió algún script; si no, responde "Sin cambios"
\`\`\`

No lleva nombre de módulo: toda la base se arma con un solo \`orden.txt\`. Si cambió algo, aparece el archivo nuevo
en \`versiones/\` y se actualizan \`historial.txt\` y este README.

## Comandos rápidos

Desde la **raíz del repo**, en **Git Bash** (Windows) o en la terminal (Linux/macOS):

| Para qué | Comando |
|---|---|
| **Generar una versión nueva del script** | \`./db/consolidado/build_schema.sh\` |
| Forzar versión nueva aunque no cambie nada | \`./db/consolidado/build_schema.sh --force\` |
| Crear tu base local vacía | \`createdb -U postgres campusbite\` |
| Cargar la versión actual en tu base local | \`psql -U postgres -d campusbite -f db/consolidado/versiones/campusbite_$version.sql\` |
| Ver qué versión tiene tu base | \`psql -U postgres -d campusbite -c "SELECT obj_description('public'::regnamespace);"\` |
| Correr las 16 verificaciones de Joseph | \`psql -U postgres -d campusbite -f db/tests/smoke_test.sql\` |
| Borrar tu base local para empezar de cero | \`dropdb -U postgres campusbite\` |

> En PowerShell o CMD no corre \`./build_schema.sh\`: usa Git Bash, o \`bash db/consolidado/build_schema.sh\`.

## Probar en local

Requisito: PostgreSQL 14 o superior con \`psql\` en el PATH.

\`\`\`bash
createdb -U postgres campusbite
psql -U postgres -d campusbite -f db/consolidado/versiones/campusbite_$version.sql
\`\`\`

En el \`.env\` de la raíz del repo, la conexión local ya viene así en \`.env.example\`:

\`\`\`
ConnectionStrings__Local=Host=localhost;Port=5432;Database=campusbite;Username=app_backend;Password=app_backend_dev
\`\`\`

- El script corre en **una sola transacción**: si algo falla, la base queda como estaba.
- Si la base **ya tiene** el esquema, se detiene sin cambiar nada. Para cargar una versión nueva:
  \`dropdb -U postgres campusbite\`, \`createdb -U postgres campusbite\` y vuelve a cargar el script.
- Incluye la clave de desarrollo \`app_backend_dev\` y datos de prueba: **nunca** lo corras en un servidor real.

## Generar una versión nueva (cuando cambia la base)

1. Crea la migración en \`db/changelog/migrations/\` y su changeSet en \`changelog-master.xml\` (flujo de Liquibase).
2. Agrega la ruta del archivo en \`orden.txt\`, en el lugar donde debe ejecutarse.
3. Genera la versión:
   \`\`\`bash
   ./db/consolidado/build_schema.sh
   \`\`\`
   Salida esperada: \`Versión V$PREFIJO.NNN generada\` con la ruta del archivo nuevo en \`versiones/\`.
4. Sube a Git los scripts nuevos, \`orden.txt\`, \`historial.txt\`, este \`README.md\` y el archivo nuevo de \`versiones/\`.

Reglas del consolidador:
- La versión es \`V$PREFIJO.NNN\`: el último número sube **1** en cada versión.
- Solo genera versión si cambió el contenido de algún script (compara la huella); si no, responde
  "Sin cambios..." y solo actualiza este README. Para forzarla: \`--force\`.
- Se detiene si \`orden.txt\` menciona un archivo que no existe, o si falta alguna migración que sí está en
  \`changelog-master.xml\`.
- No edites migraciones ya aplicadas: Liquibase las protege por checksum. Los cambios van en una migración nueva.

## Historial de versiones

| Versión | Fecha y hora | Huella | Scripts | Autor |
|---|---|---|---|---|
EOF
    grep -E '^V[0-9]' "$HISTORIAL" | sort -t'|' -k1,1r | while IFS='|' read -r v f h l a; do
      echo "| \`$v\` | $f | \`$h\` | ${l//,/, } | $a |"
    done
  } > "$README"
}

# --- 1. Leer el manifiesto -----------------------------------------------
[[ -f "$MANIFEST" ]] || error "No existe $MANIFEST"
SCRIPTS=()
while IFS= read -r linea || [[ -n "$linea" ]]; do
  linea="${linea%$'\r'}"                       # tolera finales de línea de Windows
  linea="$(echo "$linea" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//')"
  [[ -z "$linea" || "$linea" == \#* ]] && continue
  [[ -f "$DB_DIR/$linea" ]] || error "orden.txt menciona '$linea', pero no existe en db/"
  SCRIPTS+=("$linea")
done < "$MANIFEST"
[[ ${#SCRIPTS[@]} -gt 0 ]] || error "orden.txt no tiene scripts"

# --- 2. Validar que no falte ninguna migración del changelog de Liquibase -----
if [[ -f "$CHANGELOG" ]]; then
  while IFS= read -r ruta; do
    [[ " ${SCRIPTS[*]} " == *" changelog/$ruta "* ]] || \
      error "changelog-master.xml incluye 'changelog/$ruta', pero falta en orden.txt"
  done < <(grep -oE 'sqlFile path="migrations/V[^"/]+\.sql"' "$CHANGELOG" | sed -E 's/sqlFile path="([^"]+)"/\1/')
fi

# --- 3. Huella del contenido (sin \r para que Windows y Linux den lo mismo) ---
HASH="$(for s in "${SCRIPTS[@]}"; do echo "== $s"; tr -d '\r' < "$DB_DIR/$s"; done | sha256sum | cut -c1-12)"

# --- 4. Calcular la versión siguiente -----------------------------------------
ULTIMA="$( [[ -f "$HISTORIAL" ]] && grep -E '^V[0-9]' "$HISTORIAL" | tail -1 || true )"
ULTIMO_NUM=0
if [[ -n "$ULTIMA" ]]; then
  ULTIMA_VERSION="${ULTIMA%%|*}"
  ULTIMO_NUM=$((10#${ULTIMA_VERSION##*.}))
  ULTIMO_HASH="$(echo "$ULTIMA" | cut -d'|' -f3)"
  if [[ "$ULTIMO_HASH" == "$HASH" && "$FORZAR" == false ]]; then
    generar_readme
    echo "Sin cambios en los scripts desde $ULTIMA_VERSION. No se genera versión nueva (usa --force para forzarla)."
    echo "README actualizado: $README"
    exit 0
  fi
fi
NUM="$(printf '%03d' $((ULTIMO_NUM + 1)))"
VERSION="V$PREFIJO.$NUM"
FECHA="$(date '+%Y-%m-%d %H:%M:%S %z')"
AUTOR="$(git config user.name 2>/dev/null || echo "desconocido")"
LISTA="$(printf '%s\n' "${SCRIPTS[@]}" | sed -E 's#.*/##; s#\.sql$##' | paste -sd ',' -)"
SALIDA="$VERSIONES/campusbite_$VERSION.sql"
mkdir -p "$VERSIONES"

# --- 5. Escribir el script consolidado -----------------------------------------
{
  echo "-- ====================================================================="
  echo "-- CampusBite · Script consolidado de base de datos"
  echo "-- Versión : $VERSION"
  echo "-- Fecha   : $FECHA"
  echo "-- Autor   : $AUTOR"
  echo "-- Huella  : $HASH"
  echo "-- Incluye : ${#SCRIPTS[@]} scripts (ver orden.txt)"
  for s in "${SCRIPTS[@]}"; do echo "--   - $s"; done
  echo "--"
  echo "-- SOLO PARA DESARROLLO LOCAL, sobre una base VACÍA:"
  echo "--   createdb -U postgres campusbite"
  echo "--   psql -U postgres -d campusbite -f $(basename "$SALIDA")"
  echo "-- Corre todo en una transacción: si algo falla, no queda nada a medias."
  echo "-- Si la base ya tiene el esquema, se detiene sin cambiar nada (protege bases existentes)."
  echo "-- Producción NO usa este archivo: se migra con Liquibase (db/changelog)."
  echo "-- ====================================================================="
  echo "\\set ON_ERROR_STOP on"
  echo "SET client_encoding = 'UTF8';"
  echo
  echo "BEGIN;"
  echo
  echo "-- Protección: abortar si la base ya tiene el esquema de CampusBite"
  echo "DO \$\$"
  echo "BEGIN"
  echo "  IF to_regclass('public.usuario') IS NOT NULL THEN"
  echo "    RAISE EXCEPTION 'La base % ya tiene el esquema de CampusBite. Este script es solo para una base vacía (local).', current_database();"
  echo "  END IF;"
  echo "END \$\$;"
  for s in "${SCRIPTS[@]}"; do
    echo
    echo "-- ---------------------------------------------------------------------"
    echo "-- ---> $s"
    echo "-- ---------------------------------------------------------------------"
    tr -d '\r' < "$DB_DIR/$s"
    echo
  done
  echo
  echo "-- Marca de versión visible con: SELECT obj_description('public'::regnamespace);"
  echo "COMMENT ON SCHEMA public IS 'CampusBite $VERSION ($FECHA)';"
  echo
  echo "COMMIT;"
  echo
  echo "\\echo '>>> CampusBite $VERSION aplicada correctamente'"
} > "$SALIDA"

# --- 6. Registrar en el historial y regenerar el README ---------------------------
if [[ ! -f "$HISTORIAL" ]]; then
  echo "# version|fecha|huella|scripts|autor   (lo escribe build_schema.sh; no editar a mano)" > "$HISTORIAL"
fi
echo "$VERSION|$FECHA|$HASH|$LISTA|$AUTOR" >> "$HISTORIAL"
generar_readme

echo "Versión $VERSION generada:"
echo "  - $SALIDA"
echo "  - $HISTORIAL"
echo "  - $README"
