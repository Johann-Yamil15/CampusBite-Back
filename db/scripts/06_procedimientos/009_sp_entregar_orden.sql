-- JY-Sprint2 09/10/2026: entrega con el código del alumno; el efectivo se cobra aquí.
--   CALL sp_entregar_orden(<id_orden>, '123456', <id_encargado>, NULL);
-- JJ-Sprint5 13/11/2026: límite de intentos (10^6 combinaciones): 5 códigos erróneos bloquean la entrega de esa
-- orden 10 minutos. El código erróneo NO lanza excepción (deshacería el contador): devuelve p_resultado
--   'entregada' | 'codigo_incorrecto' | 'bloqueada'   y el backend lo convierte en 200 / 400 / 429.
DROP PROCEDURE IF EXISTS sp_entregar_orden(UUID, VARCHAR, UUID);
CREATE OR REPLACE PROCEDURE sp_entregar_orden(
  IN  p_id_orden  UUID,
  IN  p_codigo    VARCHAR,
  IN  p_id_actor  UUID,
  OUT p_resultado VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_cafeteria UUID;
  v_estado       estado_orden;
  v_codigo       CHAR(6);
  v_bloqueo      TIMESTAMPTZ;
BEGIN
  SELECT o.id_cafeteria, o.estado, o.codigo_recogida, o.bloqueo_entrega_hasta
    INTO v_id_cafeteria, v_estado, v_codigo, v_bloqueo
    FROM orden o WHERE o.id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe' USING ERRCODE = 'no_data_found'; END IF;

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'entregar esta orden');

  IF v_estado <> 'lista' THEN RAISE EXCEPTION 'La orden no está lista para entrega'; END IF;
  IF v_bloqueo IS NOT NULL AND v_bloqueo > now() THEN
    p_resultado := 'bloqueada';
    RETURN;
  END IF;

  IF v_codigo <> p_codigo THEN
    UPDATE orden
       SET intentos_entrega      = CASE WHEN intentos_entrega + 1 >= 5 THEN 0 ELSE intentos_entrega + 1 END,
           bloqueo_entrega_hasta = CASE WHEN intentos_entrega + 1 >= 5 THEN now() + interval '10 minutes'
                                        ELSE bloqueo_entrega_hasta END
     WHERE id_orden = p_id_orden;
    p_resultado := 'codigo_incorrecto';
    RETURN;
  END IF;

  UPDATE pago SET estado = 'pagado', pagado_en = now()
   WHERE id_orden = p_id_orden AND metodo = 'efectivo' AND estado = 'pendiente';
  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE orden SET estado = 'entregada', intentos_entrega = 0, bloqueo_entrega_hasta = NULL
   WHERE id_orden = p_id_orden;
  p_resultado := 'entregada';
END $$;
