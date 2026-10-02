#!/usr/bin/env bash
# CampusBite · TEC-03 · Ejecutor de migraciones (psql + bash, sin dependencias extra)
#
# Uso:   ./migrate.sh up        aplica las migraciones pendientes
#        ./migrate.sh down      revierte la última migración aplicada
#        ./migrate.sh status    muestra aplicadas y pendientes
#
# Conexión: DATABASE_URL (postgresql://usuario:clave@host:5432/campusbite)
#           o las variables estándar PGHOST/PGPORT/PGUSER/PGPASSWORD/PGDATABASE.
set -euo pipefail
export PGOPTIONS="${PGOPTIONS:-} -c client_min_messages=warning"   # sin NOTICE de "ya existe"

DIR="$(cd "$(dirname "$0")" && pwd)"
MIG="$DIR/migrations"
PSQL=(psql -X -q -v ON_ERROR_STOP=1 -P pager=off)
[[ -n "${DATABASE_URL:-}" ]] && PSQL+=("$DATABASE_URL")

sql() { "${PSQL[@]}" -tA -c "$1"; }

ensure_table() {
  sql "CREATE TABLE IF NOT EXISTS schema_migrations (
         version     TEXT        PRIMARY KEY,
         nombre      TEXT        NOT NULL,
         checksum    TEXT        NOT NULL,
         aplicada_en TIMESTAMPTZ NOT NULL DEFAULT now());" >/dev/null
}

version_of() { basename "$1" | cut -d_ -f1; }
name_of()    { basename "$1" .sql | sed 's/^V[0-9]*__//'; }
sum_of()     { sha256sum "$1" | cut -d' ' -f1; }

cmd_up() {
  ensure_table
  local n=0
  for f in "$MIG"/V*.sql; do
    local v; v="$(version_of "$f")"
    local applied; applied="$(sql "SELECT checksum FROM schema_migrations WHERE version='$v'")"
    if [[ -n "$applied" ]]; then
      if [[ "$applied" != "$(sum_of "$f")" ]]; then
        echo "ERROR: $v ya fue aplicada pero el archivo cambió (checksum distinto)." >&2
        echo "       No edites migraciones aplicadas: crea una nueva (V00N)." >&2
        exit 1
      fi
      continue
    fi
    echo "→ Aplicando $v ($(name_of "$f"))"
    # -1 = todo el archivo en una transacción; si algo falla, no queda a medias.
    "${PSQL[@]}" -1 -f "$f"
    sql "INSERT INTO schema_migrations(version,nombre,checksum)
         VALUES ('$v','$(name_of "$f")','$(sum_of "$f")')" >/dev/null
    n=$((n+1))
  done
  echo "Listo: $n migración(es) aplicada(s)."
}

cmd_down() {
  ensure_table
  local v; v="$(sql "SELECT version FROM schema_migrations ORDER BY version DESC LIMIT 1")"
  [[ -z "$v" ]] && { echo "No hay migraciones aplicadas."; return; }
  local f; f="$(ls "$MIG"/down/"$v"__*.down.sql 2>/dev/null | head -1 || true)"
  [[ -z "$f" ]] && { echo "ERROR: no existe rollback para $v" >&2; exit 1; }
  echo "← Revirtiendo $v"
  "${PSQL[@]}" -1 -f "$f"
  # Si V001 se revirtió, schema_migrations sigue existiendo (no la toca el down).
  sql "DELETE FROM schema_migrations WHERE version='$v'" >/dev/null
}

cmd_status() {
  ensure_table
  for f in "$MIG"/V*.sql; do
    local v; v="$(version_of "$f")"
    local when; when="$(sql "SELECT to_char(aplicada_en,'YYYY-MM-DD HH24:MI') FROM schema_migrations WHERE version='$v'")"
    printf '%-6s %-28s %s\n' "$v" "$(name_of "$f")" "${when:-PENDIENTE}"
  done
}

case "${1:-}" in
  up) cmd_up ;; down) cmd_down ;; status) cmd_status ;;
  *) echo "Uso: $0 {up|down|status}" >&2; exit 2 ;;
esac
