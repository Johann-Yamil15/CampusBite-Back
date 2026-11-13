-- JY-Sprint2 09/10/2026: pedidos de una cafetería, por hora de recogida. Solo su encargado o admin_sistema.
--   CALL sp_cola_cafeteria(<id_cafeteria>, <id_encargado>, NULL, NULL);
-- JJ-Sprint5 13/11/2026: tablero del día. p_fecha (NULL = hoy en America/Mexico_City) trae todas las órdenes cuya
-- recogida cae ese día, incluidas entregadas y canceladas; si es hoy, también las activas atrasadas de días previos.
-- Forma de fn_orden_json para la cafetería: con alumno y SIN código de recogida.
DROP PROCEDURE IF EXISTS sp_cola_cafeteria(UUID, UUID, JSONB);
CREATE OR REPLACE PROCEDURE sp_cola_cafeteria(
  IN  p_id_cafeteria UUID,
  IN  p_id_actor     UUID,
  IN  p_fecha        DATE,
  OUT p_cola         JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_hoy   CONSTANT DATE := (now() AT TIME ZONE 'America/Mexico_City')::date;
  v_fecha DATE := COALESCE(p_fecha, v_hoy);
BEGIN
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'ver la cola de esta cafetería');

  SELECT COALESCE(jsonb_agg(fn_orden_json(o.id_orden, false) ORDER BY o.recogida_en), '[]'::jsonb)
    INTO p_cola
    FROM orden o
   WHERE o.id_cafeteria = p_id_cafeteria
     AND ((o.recogida_en AT TIME ZONE 'America/Mexico_City')::date = v_fecha
          OR (v_fecha = v_hoy AND o.recogida_en < now()
              AND o.estado IN ('pendiente','confirmada','en_preparacion','lista')));
END $$;
