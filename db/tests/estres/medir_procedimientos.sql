-- =====================================================================
-- JY-Sprint3 20/10/2026: mide cada procedimiento con la base llena (después de carga_volumen.sql).
-- Corre como app_backend (igual que la API) y deja todo en ROLLBACK: no cambia los datos.
--   psql -U postgres -d estres -f medir_procedimientos.sql
-- Resultado: una línea por procedimiento con llamadas, promedio, peor caso y tamaño de la respuesta.
-- =====================================================================
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL ROLE app_backend;

DO $$
DECLARE
  c_alumno  UUID := '00000000-0000-0000-0000-0000000000a1';
  c_sistema UUID := '00000000-0000-0000-0000-0000000000a3';
  v_popular UUID;     -- la cafetería con más órdenes
  v_normal  UUID;
  v_inactiva UUID;
  v_encargado UUID;
  v_json    JSONB;
  v_id      UUID;
  v_ini     TIMESTAMPTZ;
  v_ms      NUMERIC;
  v_total   NUMERIC;
  v_max     NUMERIC;
  v_n       INT;
  v_items   JSONB;
  v_total_c INT;
  v_hora    TIMESTAMPTZ := (date_trunc('day', now() AT TIME ZONE 'America/Mexico_City') + interval '1 day 13 hours')
                           AT TIME ZONE 'America/Mexico_City';

BEGIN
  RESET ROLE;   -- solo para elegir los datos de prueba
  SELECT o.id_cafeteria INTO v_popular FROM orden o GROUP BY o.id_cafeteria ORDER BY count(*) DESC LIMIT 1;
  SELECT c.id_cafeteria INTO v_normal FROM cafeteria c
   WHERE c.activa AND NOT EXISTS (SELECT 1 FROM orden o WHERE o.id_cafeteria = c.id_cafeteria
                                    AND o.estado IN ('pendiente','confirmada','en_preparacion','lista'))
   ORDER BY c.nombre DESC LIMIT 1;
  SELECT c.id_cafeteria INTO v_inactiva FROM cafeteria c WHERE NOT c.activa LIMIT 1;
  SELECT u.id_usuario INTO v_encargado FROM usuario u WHERE u.id_cafeteria = v_popular LIMIT 1;
  SELECT count(*) INTO v_n FROM orden o WHERE o.id_cafeteria = v_popular;
  RAISE NOTICE 'Cafetería popular: % órdenes', v_n;
  SELECT jsonb_agg(jsonb_build_object('id_producto', p.id_producto, 'cantidad', 1)) INTO v_items
    FROM (SELECT pr.id_producto FROM producto pr WHERE pr.id_cafeteria = v_normal AND pr.disponible LIMIT 2) p;
  SET LOCAL ROLE app_backend;

  -- sp_listar_cafeterias (todas las activas, ~9 mil)
  v_total := 0; v_max := 0;
  FOR i IN 1..10 LOOP
    v_ini := clock_timestamp(); CALL sp_listar_cafeterias(c_alumno, false, NULL, 20, 0, v_total_c, v_json);
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms  %s cafeterías, %s',
    'sp_listar (activas, pág. 1 de 20)', 10, round(v_total/10, 2), round(v_max, 2), jsonb_array_length(v_json), pg_size_pretty(octet_length(v_json::text)::bigint));

  v_total := 0; v_max := 0;
  FOR i IN 1..10 LOOP
    v_ini := clock_timestamp(); CALL sp_listar_cafeterias(c_sistema, true, NULL, 100, 4000, v_total_c, v_json);
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms  %s cafeterías, %s',
    'sp_listar (todas, pág. 41 de 100)', 10, round(v_total/10, 2), round(v_max, 2), jsonb_array_length(v_json), pg_size_pretty(octet_length(v_json::text)::bigint));

  v_total := 0; v_max := 0;
  FOR i IN 1..10 LOOP
    v_ini := clock_timestamp(); CALL sp_listar_cafeterias(c_alumno, false, '0042', 20, 0, v_total_c, v_json);
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms  %s coinciden',
    'sp_listar (búsqueda "0042")', 10, round(v_total/10, 2), round(v_max, 2), v_total_c);

  -- sp_obtener_cafeteria
  v_total := 0; v_max := 0;
  FOR i IN 1..200 LOOP
    v_ini := clock_timestamp(); CALL sp_obtener_cafeteria(CASE WHEN i % 2 = 0 THEN v_popular ELSE v_inactiva END, c_sistema, v_json);
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms', 'sp_obtener_cafeteria', 200, round(v_total/200, 3), round(v_max, 2));

  -- sp_crear_cafeteria
  v_total := 0; v_max := 0;
  FOR i IN 1..200 LOOP
    v_ini := clock_timestamp();
    CALL sp_crear_cafeteria(c_sistema, format('Medición %s', i), 'Edificio M', true, 'Puerta', ARRAY['efectivo','tarjeta']::metodo_pago[], v_id);
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms', 'sp_crear_cafeteria', 200, round(v_total/200, 3), round(v_max, 2));

  -- sp_actualizar_cafeteria (la popular, por su encargado)
  v_total := 0; v_max := 0;
  FOR i IN 1..200 LOOP
    v_ini := clock_timestamp();
    CALL sp_actualizar_cafeteria(v_popular, v_encargado, format('Popular %s', i % 2), 'Edificio P', true, format('Ventanilla %s', i),
      CASE WHEN i % 2 = 0 THEN ARRAY['efectivo']::metodo_pago[] ELSE ARRAY['efectivo','tarjeta','transferencia']::metodo_pago[] END);
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms', 'sp_actualizar_cafeteria', 200, round(v_total/200, 3), round(v_max, 2));

  -- sp_cambiar_activa_cafeteria: la popular tiene órdenes en curso (cuenta entre todas sus órdenes y la rechaza)
  v_total := 0; v_max := 0;
  FOR i IN 1..50 LOOP
    v_ini := clock_timestamp();
    BEGIN CALL sp_cambiar_activa_cafeteria(v_popular, false, c_sistema); EXCEPTION WHEN object_in_use THEN NULL; END;
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms', 'sp_cambiar_activa (popular, 409)', 50, round(v_total/50, 3), round(v_max, 2));

  v_total := 0; v_max := 0;
  FOR i IN 1..100 LOOP
    v_ini := clock_timestamp(); CALL sp_cambiar_activa_cafeteria(v_normal, i % 2 = 0, c_sistema);
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms', 'sp_cambiar_activa (normal)', 100, round(v_total/100, 3), round(v_max, 2));

  -- Lo que ya existía, con este volumen
  v_total := 0; v_max := 0;
  FOR i IN 1..100 LOOP
    v_ini := clock_timestamp(); CALL sp_menu_cafeteria(v_popular, v_json);
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms', 'sp_menu_cafeteria (una)', 100, round(v_total/100, 3), round(v_max, 2));

  v_total := 0; v_max := 0;
  FOR i IN 1..100 LOOP
    v_ini := clock_timestamp();
    CALL sp_crear_orden(c_alumno, v_normal, v_hora, 'efectivo', v_items, NULL, v_id);
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms', 'sp_crear_orden', 100, round(v_total/100, 3), round(v_max, 2));

  v_total := 0; v_max := 0;
  FOR i IN 1..20 LOOP
    v_ini := clock_timestamp(); CALL sp_cola_cafeteria(v_popular, v_encargado, v_json);
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms  %s órdenes en cola', 'sp_cola_cafeteria (popular)', 20, round(v_total/20, 2), round(v_max, 2), jsonb_array_length(v_json));

  v_total := 0; v_max := 0;
  FOR i IN 1..20 LOOP
    v_ini := clock_timestamp(); CALL sp_bitacora_registro(c_sistema, 'cafeteria', v_popular::text, 50, v_json);
    v_ms := extract(epoch FROM clock_timestamp() - v_ini) * 1000; v_total := v_total + v_ms; v_max := greatest(v_max, v_ms);
  END LOOP;
  RAISE NOTICE '%', format('%-34s %5s llamadas  prom %8s ms  peor %8s ms  (entre 2 millones)', 'sp_bitacora_registro', 20, round(v_total/20, 3), round(v_max, 2));
END $$;

ROLLBACK;
