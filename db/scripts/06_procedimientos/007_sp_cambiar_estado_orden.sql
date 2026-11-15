-- JY-Sprint2 09/10/2026: la cafetería avanza la orden a en_preparacion, lista o cancelada.
--   CALL sp_cambiar_estado_orden(<id_orden>, 'en_preparacion', <id_encargado>, NULL);
-- JJ-Sprint5 13/11/2026: p_motivo (obligatorio al cancelar, 5..200) se guarda en orden.motivo_cancelacion; y una
-- orden "pendiente" (falta el pago) no se puede preparar, con un mensaje claro en vez del genérico del trigger.
DROP PROCEDURE IF EXISTS sp_cambiar_estado_orden(UUID, estado_orden, UUID);
CREATE OR REPLACE PROCEDURE sp_cambiar_estado_orden(
  IN p_id_orden UUID,
  IN p_nuevo    estado_orden,
  IN p_id_actor UUID,
  IN p_motivo   VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_cafeteria UUID;
  v_estado       estado_orden;
BEGIN
  IF p_nuevo NOT IN ('en_preparacion','lista','cancelada') THEN
    RAISE EXCEPTION 'Estado no permitido en este procedimiento: %', p_nuevo;
  END IF;

  SELECT o.id_cafeteria, o.estado INTO v_id_cafeteria, v_estado
    FROM orden o WHERE o.id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe' USING ERRCODE = 'no_data_found'; END IF;

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'modificar esta orden');

  IF p_nuevo = 'en_preparacion' AND v_estado = 'pendiente' THEN
    RAISE EXCEPTION 'La orden está esperando la confirmación del pago';
  END IF;
  IF p_nuevo = 'cancelada' AND length(btrim(COALESCE(p_motivo, ''))) NOT BETWEEN 5 AND 200 THEN
    RAISE EXCEPTION 'El motivo debe tener entre 5 y 200 caracteres';
  END IF;

  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE orden
     SET estado = p_nuevo,
         motivo_cancelacion = CASE WHEN p_nuevo = 'cancelada' THEN btrim(p_motivo) ELSE motivo_cancelacion END
   WHERE id_orden = p_id_orden;
END $$;
