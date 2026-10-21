#!/usr/bin/env bash
# =====================================================================
# JY-Sprint3 20/10/2026: pruebas de mutación de la BD.
# Para cada mutante de mutantes.txt: copia una base sana, le aplica el script con el error puesto y corre
# db/tests/smoke_test.sql. Si el smoke test falla, el mutante muere (bien: las pruebas detectan ese error).
# Si pasa, sobrevive (falta una prueba para ese caso).
#
# Uso (Git Bash / Linux), desde la raíz del repo, con una PostgreSQL a la mano:
#   PSQL="psql -U postgres -h localhost" ./db/tests/mutacion/probar_mutantes.sh
#   PSQL="podman exec -i mi-postgres psql -U postgres" ./db/tests/mutacion/probar_mutantes.sh     # contenedor
# Opcional: SOLO=M06 para correr un solo mutante.
# Crea y borra las bases mutacion_base y mutacion_m; no toca ninguna otra.
# =====================================================================
set -uo pipefail

DIR="$(cd "$(dirname "$0")" && pwd)"
DB_DIR="$(cd "$DIR/../.." && pwd)"
PSQL="${PSQL:-psql -U postgres}"
SOLO="${SOLO:-}"
VERSION="$(ls "$DB_DIR/consolidado/versiones" | grep -E '^campusbite_V[0-9.]+\.sql$' | sort -V | tail -1)"
SMOKE="$DB_DIR/tests/smoke_test.sql"

sql() { $PSQL -q -v ON_ERROR_STOP=1 "$@"; }
recortar() { local t="$1"; t="${t#"${t%%[![:space:]]*}"}"; echo "${t%"${t##*[![:space:]]}"}"; }

echo "Base sana: $VERSION"
sql -d postgres -c "DROP DATABASE IF EXISTS mutacion_m" -c "DROP DATABASE IF EXISTS mutacion_base" -c "CREATE DATABASE mutacion_base" >/dev/null 2>&1
sql -d mutacion_base < "$DB_DIR/consolidado/versiones/$VERSION" >/dev/null 2>&1 || { echo "ERROR: no se pudo crear la base sana"; exit 2; }
sql -d mutacion_base < "$SMOKE" >/dev/null 2>&1 || { echo "ERROR: el smoke test falla SIN mutantes; arréglalo antes"; exit 2; }

muertos=0; vivos=0; vivos_conc=0; no_aplica=0
# Se lee por el descriptor 3: psql (o "podman exec -i") dentro del ciclo consumiría la entrada estándar
while IFS= read -r linea <&3 || [[ -n "$linea" ]]; do
  linea="${linea%$'\r'}"
  [[ "$linea" =~ ^M[0-9]+ ]] || continue
  IFS=$'\x1f' read -r id archivo original mutado descripcion <<< "${linea//|||/$'\x1f'}"
  id="$(recortar "$id")"; archivo="$(recortar "$archivo")"; original="$(recortar "$original")"
  mutado="$(recortar "$mutado")"; descripcion="$(recortar "$descripcion")"
  [[ -n "$SOLO" && "$id" != "$SOLO" ]] && continue

  contenido="$(tr -d '\r' < "$DB_DIR/scripts/$archivo")"
  if [[ "$contenido" != *"$original"* ]]; then
    printf '%-4s %-11s %s\n' "$id" "NO APLICA" "(el texto original ya no está en $archivo)"
    no_aplica=$((no_aplica + 1)); continue
  fi

  if [[ "$archivo" =~ ^0[123]_ ]]; then
    # Scripts de esquema (tablas, cambios): solo actúan en bases que aún no tienen el cambio (IF NOT EXISTS),
    # así que aplicarlos sobre la base sana no hace nada. Se arma una base NUEVA con el consolidado mutado.
    copia="$(mktemp -d)"; cp -r "$DB_DIR/." "$copia/"
    printf '%s\n' "${contenido//"$original"/"$mutado"}" > "$copia/scripts/$archivo"
    ( cd "$copia" && bash consolidado/build_schema.sh --force >/dev/null 2>&1 )
    mutada="$(ls "$copia/consolidado/versiones" | grep -E '^campusbite_V[0-9.]+\.sql$' | sort -V | tail -1)"
    sql -d postgres -c "DROP DATABASE IF EXISTS mutacion_m" -c "CREATE DATABASE mutacion_m" >/dev/null 2>&1
    sql -d mutacion_m < "$copia/consolidado/versiones/$mutada" >/dev/null 2>&1; aplicado=$?
    rm -rf "$copia"
  else
    sql -d postgres -c "DROP DATABASE IF EXISTS mutacion_m" -c "CREATE DATABASE mutacion_m TEMPLATE mutacion_base" >/dev/null 2>&1
    printf '%s\n' "${contenido//"$original"/"$mutado"}" | sql -d mutacion_m >/dev/null 2>&1; aplicado=$?
  fi
  if [[ $aplicado -ne 0 ]]; then
    estado="MUERTO"     # el script mutado ni siquiera se puede aplicar
  elif sql -d mutacion_m < "$SMOKE" >/dev/null 2>&1; then
    estado="SOBREVIVE"
  else
    estado="MUERTO"
  fi

  if [[ "$estado" == "MUERTO" ]]; then
    muertos=$((muertos + 1))
  elif [[ "$descripcion" == *"[concurrencia]"* ]]; then
    vivos_conc=$((vivos_conc + 1)); estado="SOBREVIVE*"
  else
    vivos=$((vivos + 1))
  fi
  printf '%-4s %-11s %s\n' "$id" "$estado" "$descripcion"
done 3< "$DIR/mutantes.txt"

sql -d postgres -c "DROP DATABASE IF EXISTS mutacion_m" -c "DROP DATABASE IF EXISTS mutacion_base" >/dev/null 2>&1
total=$((muertos + vivos + vivos_conc))
echo
echo "Muertos: $muertos de $total  ·  sobreviven: $vivos  ·  sobreviven de concurrencia (*): $vivos_conc  ·  no aplican: $no_aplica"
echo "(*) El smoke test usa una sola sesión: estos los debe matar db/tests/estres/concurrencia.sh"
[[ $vivos -eq 0 && $no_aplica -eq 0 ]]
