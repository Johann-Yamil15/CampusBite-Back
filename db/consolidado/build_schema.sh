#!/usr/bin/env bash
# =====================================================================
# JJ-Sprint2 09/10/2026: consolidador versionado de los scripts de BD de CampusBite
# JY-Sprint2 09/10/2026: módulos con su propio orden.txt, script _prod sin datos de prueba,
#                        changelog de Liquibase generado y scripts idempotentes (sin candado de base vacía)
#
# Une en UN solo archivo .sql los scripts de los módulos listados en orden.txt, con encabezado de versión
# (V1.0.0.NNN, el último número sube 1 en cada versión), fecha, hora, autor y huella (hash).
# Genera además:
#   - versiones/campusbite_V..._prod.sql   lo mismo sin los módulos de desarrollo (para producción)
#   - ../changelog/changelog-master.xml    un changeSet por script, para Liquibase en local
#   - README.md                            con el historial de versiones
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

# Quita \r (Windows) y espacios de los extremos
limpiar() { local l="${1%$'\r'}"; l="${l#"${l%%[![:space:]]*}"}"; echo "${l%"${l##*[![:space:]]}"}"; }

# JJ-Sprint2 09/10/2026: README generado a partir de la última línea del historial (se llama siempre)
generar_readme() {
  local ultima version fecha autor
  ultima="$(grep -E '^V[0-9]' "$HISTORIAL" | tail -1)"
  version="$(echo "$ultima" | cut -d'|' -f1)"
  fecha="$(echo "$ultima" | cut -d'|' -f2)"
  autor="$(echo "$ultima" | cut -d'|' -f5)"
  {
    cat <<EOF
# Script consolidado de la base de datos

> Este archivo lo genera \`build_schema.sh\`. No lo edites a mano: se reescribe cada vez que se ejecuta.

**Versión actual: \`$version\`** · generada el $fecha por $autor

| Archivo | Para qué |
|---|---|
| [\`versiones/campusbite_$version.sql\`](versiones/campusbite_$version.sql) | Local: esquema + clave de desarrollo de \`app_backend\` + datos de prueba |
| [\`versiones/campusbite_${version}_prod.sql\`](versiones/campusbite_${version}_prod.sql) | Producción (Neon): solo esquema, sin datos de prueba ni claves |

Los dos son **idempotentes**: se pueden ejecutar sobre una base vacía o sobre una que ya tenga una versión
anterior (incluida V1.0.0.001). Cada script revisa antes de crear o cambiar algo, así que repetirlo no
falla ni duplica nada, y los datos existentes se conservan. Todo corre en una transacción: si algo falla,
la base queda como estaba.


Desde la **raíz del repo**, en **Git Bash** (Windows) o en la terminal (Linux/macOS):

| Para qué | Comando |
|---|---|
| **Generar una versión nueva del script** | \`./db/consolidado/build_schema.sh\` |
| Forzar versión nueva aunque no cambie nada | \`./db/consolidado/build_schema.sh --force\` |
| Crear tu base local vacía | \`createdb -U postgres campusbite\` |
| Cargar o actualizar tu base local | \`psql -U postgres -d campusbite -f db/consolidado/versiones/campusbite_$version.sql\` |
| Actualizar producción (Neon) | \`psql "<cadena de neondb_owner>" -f db/consolidado/versiones/campusbite_${version}_prod.sql\` |
| Ver qué versión tiene una base | \`psql -U postgres -d campusbite -c "SELECT obj_description('public'::regnamespace);"\` |
| Correr las pruebas de humo | \`psql -U postgres -d campusbite -f db/tests/smoke_test.sql\` |
| Borrar tu base local para empezar de cero | \`dropdb -U postgres campusbite\` |

> En PowerShell o CMD no corre \`./build_schema.sh\`: usa Git Bash, o \`bash db/consolidado/build_schema.sh\`.

Requisito: PostgreSQL 14 o superior (usa \`CREATE OR REPLACE TRIGGER\` y procedimientos con \`OUT\`).

En el \`.env\` de la raíz del repo, la conexión local ya viene así en \`.env.example\`:

\`\`\`
ConnectionStrings__Local=Host=localhost;Port=5432;Database=campusbite;Username=app_backend;Password=app_backend_dev
\`\`\`

- El archivo local incluye la clave de desarrollo \`app_backend_dev\` y datos de prueba: **nunca** lo corras en un servidor real.
- El archivo \`_prod\` no toca la clave de \`app_backend\`: se asigna a mano (ver \`db/README.md\`).

## Generar la versión del script (manual, cuando se ocupe)

El script **no se ejecuta solo**: genera una versión únicamente cuando alguien corre estos comandos.
En **Git Bash** (Windows) o en la terminal (Linux/macOS):

\`\`\`bash
cd db/consolidado
chmod +x build_schema.sh      # solo la primera vez (da permiso de ejecución)
./build_schema.sh             # genera V$PREFIJO.NNN si cambió algún script; si no, responde "Sin cambios"
\`\`\`

No lleva nombre de módulo: arma toda la base siguiendo \`orden.txt\` (módulos) y el \`orden.txt\` de cada módulo.
Si cambió algo, aparecen los dos archivos nuevos en \`versiones/\` y se actualizan \`historial.txt\`, este README y
\`db/changelog/changelog-master.xml\`.

## Cómo se organiza

\`\`\`
db/
  consolidado/orden.txt        módulos en el orden en que se ejecutan
  scripts/<módulo>/orden.txt   scripts del módulo en el orden en que se ejecutan
  scripts/<módulo>/NNN_*.sql   un cambio por archivo, idempotente
\`\`\`

Directivas en el \`orden.txt\` de un módulo (líneas de comentario):
- \`# liquibase: context=dev\`: el módulo es solo de desarrollo. Queda fuera del archivo \`_prod\` y Liquibase lo
  aplica solo con el contexto \`dev\`.
- \`# liquibase: runAlways\`: Liquibase lo ejecuta en cada \`update\` (se usa en los permisos).

## Generar una versión nueva (cuando cambia la base)

1. Crea el script en el módulo que corresponda, por ejemplo \`db/scripts/03_cambios_sprint2/006_agregar_columna_x.sql\`,
   usando \`IF NOT EXISTS\`, \`CREATE OR REPLACE\` o un bloque \`DO \$\$\` que revise antes de cambiar.
2. Agrega el nombre del archivo al final del \`orden.txt\` de ese módulo. Si es un módulo nuevo, agrega su carpeta
   en \`db/consolidado/orden.txt\`.
3. Genera la versión:
   \`\`\`bash
   ./db/consolidado/build_schema.sh
   \`\`\`
   Salida esperada: \`Versión V$PREFIJO.NNN generada\` con las rutas de los archivos nuevos.
4. Sube a Git los scripts nuevos, los \`orden.txt\`, \`historial.txt\`, este \`README.md\`, \`changelog-master.xml\`
   y los dos archivos nuevos de \`versiones/\`.

Reglas del consolidador:
- La versión es \`V$PREFIJO.NNN\`: el último número sube **1** en cada versión.
- Solo genera versión si cambió el contenido o el orden de algún script (compara la huella); si no, responde
  "Sin cambios..." y solo actualiza este README. Para forzarla: \`--force\`.
- Se detiene si un \`orden.txt\` menciona un archivo o carpeta que no existe, o si un script de una carpeta de módulo
  no está en su \`orden.txt\` (para que no se quede nada fuera por olvido).
- Las tablas (\`02_tablas\`) no se editan una vez publicadas: un cambio de columna va en un script nuevo para que
  las bases existentes también lo reciban. Triggers, vistas, procedimientos y permisos sí se editan en su archivo.

## Historial de versiones

| Versión | Fecha y hora | Huella | Scripts | Autor |
|---|---|---|---|---|
EOF
    grep -E '^V[0-9]' "$HISTORIAL" | sort -t'|' -k1,1r | while IFS='|' read -r v f h l a; do
      echo "| \`$v\` | $f | \`$h\` | ${l//,/, } | $a |"
    done
  } > "$README"
}

# --- 1. Leer el manifiesto: módulos (carpetas con su orden.txt) o archivos sueltos ---------
[[ -f "$MANIFEST" ]] || error "No existe $MANIFEST"
SCRIPTS=()      # rutas relativas a db/
CONTEXTOS=()    # "dev" o vacío, por script
SIEMPRE=()      # "true" o "false" (runAlways de Liquibase), por script

agregar_modulo() {
  local modulo="$1" orden="$DB_DIR/$1/orden.txt" linea contexto="" siempre=false archivo
  [[ -f "$orden" ]] || error "El módulo '$modulo' no tiene orden.txt"
  grep -qiE '^#[[:space:]]*liquibase:[[:space:]]*context=dev' "$orden" && contexto="dev"
  grep -qiE '^#[[:space:]]*liquibase:[[:space:]]*runAlways' "$orden" && siempre=true
  while IFS= read -r linea || [[ -n "$linea" ]]; do
    linea="$(limpiar "$linea")"
    [[ -z "$linea" || "$linea" == \#* ]] && continue
    [[ -f "$DB_DIR/$modulo/$linea" ]] || error "$modulo/orden.txt menciona '$linea', pero no existe"
    SCRIPTS+=("$modulo/$linea"); CONTEXTOS+=("$contexto"); SIEMPRE+=("$siempre")
  done < "$orden"
  # Ningún .sql del módulo puede quedarse fuera de su orden.txt
  for archivo in "$DB_DIR/$modulo"/*.sql; do
    [[ -e "$archivo" ]] || continue
    grep -qxF "$(basename "$archivo")" <(tr -d '\r' < "$orden" | sed 's/^[[:space:]]*//; s/[[:space:]]*$//') || \
      error "'$modulo/$(basename "$archivo")' no está en $modulo/orden.txt"
  done
}

while IFS= read -r linea || [[ -n "$linea" ]]; do
  linea="$(limpiar "$linea")"
  [[ -z "$linea" || "$linea" == \#* ]] && continue
  if [[ -d "$DB_DIR/$linea" ]]; then
    agregar_modulo "${linea%/}"
  elif [[ -f "$DB_DIR/$linea" ]]; then
    SCRIPTS+=("$linea"); CONTEXTOS+=(""); SIEMPRE+=(false)
  else
    error "orden.txt menciona '$linea', pero no existe en db/"
  fi
done < "$MANIFEST"
[[ ${#SCRIPTS[@]} -gt 0 ]] || error "orden.txt no tiene scripts"

# --- 2. Huella del contenido y del orden (sin \r para que Windows y Linux den lo mismo) ----
HASH="$(for s in "${SCRIPTS[@]}"; do echo "== $s"; tr -d '\r' < "$DB_DIR/$s"; done | sha256sum | cut -c1-12)"

# --- 3. Changelog de Liquibase (local): siempre se regenera desde los orden.txt ---------------
generar_changelog() {
  local i s ctx run
  {
    echo '<?xml version="1.0" encoding="UTF-8"?>'
    echo '<!-- Generado por db/consolidado/build_schema.sh a partir de los orden.txt. NO editar a mano. -->'
    echo '<databaseChangeLog'
    echo '    xmlns="http://www.liquibase.org/xml/ns/dbchangelog"'
    echo '    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"'
    echo '    xsi:schemaLocation="http://www.liquibase.org/xml/ns/dbchangelog'
    echo '                        http://www.liquibase.org/xml/ns/dbchangelog/dbchangelog-latest.xsd">'
    echo
    echo '    <!-- Liquibase se usa solo en local (Docker). Los scripts son idempotentes: runOnChange vuelve a'
    echo '         ejecutar un script cuando cambia su contenido. Para revertir en local: docker compose down -v. -->'
    for i in "${!SCRIPTS[@]}"; do
      s="${SCRIPTS[$i]}"
      ctx=""; [[ -n "${CONTEXTOS[$i]}" ]] && ctx=" context=\"${CONTEXTOS[$i]}\""
      run="runOnChange=\"true\""; [[ "${SIEMPRE[$i]}" == true ]] && run="runAlways=\"true\""
      echo
      echo "    <changeSet id=\"$s\" author=\"campusbite\" $run$ctx>"
      echo "        <sqlFile path=\"$s\" splitStatements=\"false\" stripComments=\"false\"/>"
      echo "        <rollback/>"
      echo "    </changeSet>"
    done
    echo '</databaseChangeLog>'
  } > "$CHANGELOG"
}
generar_changelog

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
    echo "README y changelog actualizados."
    exit 0
  fi
fi
NUM="$(printf '%03d' $((ULTIMO_NUM + 1)))"
VERSION="V$PREFIJO.$NUM"
FECHA="$(date '+%Y-%m-%d %H:%M:%S %z')"
AUTOR="$(git config user.name 2>/dev/null || echo "desconocido")"
LISTA="$(printf '%s\n' "${SCRIPTS[@]}" | sed -E 's#^scripts/##; s#\.sql$##' | paste -sd ',' -)"
SALIDA="$VERSIONES/campusbite_$VERSION.sql"
SALIDA_PROD="$VERSIONES/campusbite_${VERSION}_prod.sql"
mkdir -p "$VERSIONES"

# --- 5. Escribir los scripts consolidados -----------------------------------------
# $1 = archivo de salida, $2 = "prod" para dejar fuera los módulos de desarrollo
escribir_consolidado() {
  local salida="$1" modo="$2" i s n=0
  for i in "${!SCRIPTS[@]}"; do [[ "$modo" == prod && -n "${CONTEXTOS[$i]}" ]] || n=$((n + 1)); done
  {
    echo "-- ====================================================================="
    echo "-- CampusBite · Script consolidado de base de datos"
    echo "-- Versión : $VERSION$([[ "$modo" == prod ]] && echo " (producción: sin datos de prueba)")"
    echo "-- Fecha   : $FECHA"
    echo "-- Autor   : $AUTOR"
    echo "-- Huella  : $HASH"
    echo "-- Incluye : $n scripts (ver orden.txt de cada módulo)"
    for i in "${!SCRIPTS[@]}"; do
      [[ "$modo" == prod && -n "${CONTEXTOS[$i]}" ]] && continue
      echo "--   - ${SCRIPTS[$i]}"
    done
    echo "--"
    echo "-- Idempotente: se puede ejecutar sobre una base vacía o sobre una versión anterior; repetirlo no"
    echo "-- falla ni duplica nada. Corre todo en una transacción: si algo falla, no queda nada a medias."
    if [[ "$modo" == prod ]]; then
      echo "--   psql \"<cadena de neondb_owner>\" -f $(basename "$salida")"
    else
      echo "-- SOLO PARA DESARROLLO LOCAL (trae la clave pública de app_backend y datos de prueba):"
      echo "--   psql -U postgres -d campusbite -f $(basename "$salida")"
    fi
    echo "-- ====================================================================="
    echo "\\set ON_ERROR_STOP on"
    echo "SET client_encoding = 'UTF8';"
    echo
    echo "BEGIN;"
    for i in "${!SCRIPTS[@]}"; do
      s="${SCRIPTS[$i]}"
      [[ "$modo" == prod && -n "${CONTEXTOS[$i]}" ]] && continue
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
  } > "$salida"
}
escribir_consolidado "$SALIDA" local
escribir_consolidado "$SALIDA_PROD" prod

# --- 6. Registrar en el historial y regenerar el README ---------------------------
if [[ ! -f "$HISTORIAL" ]]; then
  echo "# version|fecha|huella|scripts|autor   (lo escribe build_schema.sh; no editar a mano)" > "$HISTORIAL"
fi
echo "$VERSION|$FECHA|$HASH|$LISTA|$AUTOR" >> "$HISTORIAL"
generar_readme

echo "Versión $VERSION generada:"
echo "  - $SALIDA"
echo "  - $SALIDA_PROD"
echo "  - $HISTORIAL"
echo "  - $README"
echo "  - $CHANGELOG"
