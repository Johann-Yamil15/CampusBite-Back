#!/usr/bin/env bash
# =====================================================================
# JY-Sprint3 20/10/2026: prueba de concurrencia de TEC-08 con pgbench (muchas sesiones a la vez).
#  1. Órdenes vs. bajas: 16 sesiones de app_backend crean órdenes sin parar en 5 cafeterías mientras un admin
#     las da de baja y las reactiva una y otra vez. Regla: nunca queda una orden en curso en una cafetería
#     inactiva (la protegen FOR SHARE en sp_crear_orden y FOR UPDATE en sp_cambiar_activa_cafeteria).
#  2. Ediciones simultáneas de la misma cafetería: los métodos de pago quedan como UNA de las ediciones, no mezclados.
#  3. Altas simultáneas con el mismo nombre: queda exactamente una.
#  Además: cero deadlocks y cero errores inesperados (incluye lock_timeout de app_backend).
#
# Uso (Git Bash / Linux), desde la raíz del repo:
#   CONTENEDOR=mi-postgres MOTOR=podman ./db/tests/estres/concurrencia.sh     # PostgreSQL en un contenedor
#   PSQL="psql -U postgres" PGBENCH="pgbench" ./db/tests/estres/concurrencia.sh   # PostgreSQL instalado
# Opcional: SEGUNDOS=30 (duración), MUTANTE=M14 (aplica un mutante de db/tests/mutacion/mutantes.txt para
# comprobar que esta prueba lo detecta).
# Crea y borra la base "concurrencia".
# =====================================================================
set -uo pipefail
# En Git Bash (Windows), MSYS convierte rutas como /tmp/... al pasarlas a docker/podman: se desactiva
export MSYS_NO_PATHCONV=1
DIR="$(cd "$(dirname "$0")" && pwd)"
DB_DIR="$(cd "$DIR/../.." && pwd)"
SEGUNDOS="${SEGUNDOS:-25}"
MUTANTE="${MUTANTE:-}"
MOTOR="${MOTOR:-docker}"
if [[ -n "${CONTENEDOR:-}" ]]; then
  PSQL="$MOTOR exec -i $CONTENEDOR psql -U postgres"
  PGBENCH="$MOTOR exec $CONTENEDOR pgbench"
  RUTA="/tmp/campusbite_estres"
  $MOTOR exec "$CONTENEDOR" mkdir -p "$RUTA"
else
  PSQL="${PSQL:-psql -U postgres}"
  PGBENCH="${PGBENCH:-pgbench}"
  RUTA="$(mktemp -d)"
fi
sql() { $PSQL -q -v ON_ERROR_STOP=1 "$@"; }
BASE=concurrencia
VERSION="$(ls "$DB_DIR/consolidado/versiones" | grep -E '^campusbite_V[0-9.]+\.sql$' | sort -V | tail -1)"

# --- Base nueva con la versión actual (datos de prueba incluidos) y la preparación ---------------------
sql -d postgres -c "DROP DATABASE IF EXISTS $BASE" -c "CREATE DATABASE $BASE" >/dev/null 2>&1
sql -d $BASE < "$DB_DIR/consolidado/versiones/$VERSION" >/dev/null 2>&1 || { echo "ERROR al crear la base"; exit 2; }
sql -d $BASE < "$DIR/concurrencia_preparar.sql" >/dev/null || { echo "ERROR en la preparación"; exit 2; }

if [[ -n "$MUTANTE" ]]; then
  linea="$(grep -E "^$MUTANTE " "$DB_DIR/tests/mutacion/mutantes.txt")" || { echo "No existe $MUTANTE"; exit 2; }
  IFS=$'\x1f' read -r _ archivo original mutado descripcion <<< "${linea//|||/$'\x1f'}"
  archivo="$(echo "$archivo" | xargs)"; original="$(echo "$original" | sed 's/^ *//; s/ *$//')"; mutado="$(echo "$mutado" | sed 's/^ *//; s/ *$//')"
  contenido="$(tr -d '\r' < "$DB_DIR/scripts/$archivo")"
  [[ "$contenido" == *"$original"* ]] || { echo "El mutante $MUTANTE ya no aplica"; exit 2; }
  printf '%s\n' "${contenido//"$original"/"$mutado"}" | sql -d $BASE >/dev/null
  echo ">>> Corriendo CON el mutante $MUTANTE:$descripcion"
fi

# --- Scripts de pgbench --------------------------------------------------------------------------------
TMP="$(mktemp -d)"
printf '%s\n' '\set c random(1, 5)' '\set a random(1, 200)' 'CALL estres_ordenar(:c, :a);' > "$TMP/ordenar.sql"
printf '%s\n' '\set j random(1, 2)' 'CALL estres_editar(:j);' > "$TMP/editar.sql"
printf '%s\n' 'CALL estres_crear_mismo_nombre();' > "$TMP/mismo_nombre.sql"
if [[ -n "${CONTENEDOR:-}" ]]; then
  for f in ordenar editar mismo_nombre; do $MOTOR exec -i "$CONTENEDOR" sh -c "cat > $RUTA/$f.sql" < "$TMP/$f.sql"; done
else
  cp "$TMP"/*.sql "$RUTA/"
fi
DEADLOCKS_ANTES="$(sql -d postgres -At -c "SELECT deadlocks FROM pg_stat_database WHERE datname = '$BASE'")"

# --- 1. Órdenes vs. bajas ------------------------------------------------------------------------------
echo "1. $SEGUNDOS s: 16 sesiones creando órdenes (~200/s) mientras se dan de baja y reactivan 5 cafeterías"
$PGBENCH -U app_backend -n -c 16 -j 4 -T "$SEGUNDOS" --rate=200 -f "$RUTA/ordenar.sql" $BASE > "$TMP/pgbench_ordenes.txt" 2>&1 &
PGB=$!
sleep 2
for c in 1 2 3 4 5; do
  sql -d $BASE -c "CALL estres_ciclos_de_baja($c, 15)" >/dev/null 2>&1 &
done
wait
grep -E "number of transactions actually processed|latency average|tps =|number of failed" "$TMP/pgbench_ordenes.txt" | sed 's/^/   /'

# --- 2. Ediciones simultáneas --------------------------------------------------------------------------
echo "2. 10 s: 16 sesiones editando la MISMA cafetería con dos juegos de métodos de pago"
$PGBENCH -U app_backend -n -c 16 -j 4 -T 10 -f "$RUTA/editar.sql" $BASE 2>&1 | grep -E "actually processed|latency average|tps =" | sed 's/^/   /'

# --- 3. Altas simultáneas con el mismo nombre -----------------------------------------------------------
echo "3. 32 sesiones dando de alta al mismo tiempo una cafetería con el mismo nombre"
$PGBENCH -U app_backend -n -c 32 -j 4 -t 1 -f "$RUTA/mismo_nombre.sql" $BASE >/dev/null 2>&1

# --- Resultados ------------------------------------------------------------------------------------------
echo
echo "Resultados:"
sql -d $BASE -At -F ' ' -c "SELECT '   ' || lpad(count(*)::text, 7) || '  ' || resultado FROM estres_log
                              WHERE resultado NOT LIKE 'intentos de baja:%' GROUP BY resultado ORDER BY count(*) DESC"
sql -d $BASE -At -c "SELECT '   intentos por baja exitosa: promedio ' || round(avg(split_part(resultado, ': ', 2)::int), 1) ||
                            ', máximo ' || max(split_part(resultado, ': ', 2)::int)
                       FROM estres_log WHERE resultado LIKE 'intentos de baja:%'"
echo
fallas=0
revisar() {  # revisar "<descripción>" "<consulta que devuelve true si está bien>"
  local ok; ok="$(sql -d $BASE -At -c "$2")"
  if [[ "$ok" == "t" ]]; then echo "   OK     $1"; else echo "   FALLA  $1"; fallas=$((fallas + 1)); fi
}
revisar "Ninguna orden se coló en una cafetería inactiva" \
  "SELECT NOT EXISTS (SELECT 1 FROM estres_log WHERE resultado LIKE 'VIOLACIÓN%')"
revisar "Hubo bajas exitosas y órdenes rechazadas por cafetería inactiva (la prueba sí ejercitó la carrera)" \
  "SELECT (SELECT count(*) FROM estres_log WHERE resultado = 'baja exitosa') > 10
      AND (SELECT count(*) FROM estres_log WHERE resultado = 'orden rechazada: cafetería inactiva') > 0"
revisar "Sin errores inesperados (incluye lock_timeout y statement_timeout de app_backend)" \
  "SELECT NOT EXISTS (SELECT 1 FROM estres_log WHERE resultado LIKE 'ERROR%')"
revisar "pgbench no tuvo transacciones fallidas" \
  "SELECT true"   # (se revisa abajo en el texto de pgbench)
grep -qE "number of failed transactions: [1-9]" "$TMP/pgbench_ordenes.txt" && { echo "   FALLA  pgbench reportó transacciones fallidas"; fallas=$((fallas + 1)); }
revisar "Los métodos de la cafetería editada son UNO de los dos juegos, no una mezcla" \
  "SELECT (SELECT array_agg(m.metodo::text ORDER BY m.metodo::text) FROM cafeteria_metodo_pago m JOIN estres_cafes c ON c.id = m.id_cafeteria AND c.n = 1)
          IN ('{efectivo,tarjeta}', '{transferencia}')"
revisar "Ediciones aplicadas sin errores" \
  "SELECT (SELECT count(*) FROM estres_log WHERE resultado = 'edición aplicada') > 100"
revisar "De 32 altas con el mismo nombre quedó exactamente una (31 rechazadas con 409)" \
  "SELECT (SELECT count(*) FROM cafeteria WHERE nombre = 'Carrera de nombres') = 1
      AND (SELECT count(*) FROM estres_log WHERE resultado = 'alta con nombre repetido: rechazada (409)') = 31"
revisar "Cero deadlocks" \
  "SELECT deadlocks = $DEADLOCKS_ANTES FROM pg_stat_database WHERE datname = '$BASE'"
revisar "pago.monto = suma del detalle en todas las órdenes" \
  "SELECT NOT EXISTS (SELECT 1 FROM pago p JOIN (SELECT id_orden, sum(subtotal) s FROM detalle_orden GROUP BY id_orden) d USING (id_orden) WHERE p.monto <> d.s)"

sql -d postgres -c "DROP DATABASE IF EXISTS $BASE" >/dev/null 2>&1
rm -rf "$TMP"
echo
if [[ $fallas -eq 0 ]]; then echo ">>> CONCURRENCIA: todo OK"; else echo ">>> CONCURRENCIA: $fallas falla(s)"; fi
[[ $fallas -eq 0 ]]
