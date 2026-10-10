-- JY-Sprint2 09/10/2026: la cafetería avanza la orden a en_preparacion, lista o cancelada.
--   CALL sp_cambiar_estado_orden(<id_orden>, 'en_preparacion', <id_encargado>);
CREATE OR REPLACE PROCEDURE sp_cambiar_estado_orden(
  IN p_id_orden UUID,
  IN p_nuevo    estado_orden,
  IN p_id_actor UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_cafeteria UUID;
BEGIN
  IF p_nuevo NOT IN ('en_preparacion','lista','cancelada') THEN
    RAISE EXCEPTION 'Estado no permitido en este procedimiento: %', p_nuevo;
  END IF;

  SELECT o.id_cafeteria INTO v_id_cafeteria FROM orden o WHERE o.id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'modificar esta orden');

  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE orden SET estado = p_nuevo WHERE id_orden = p_id_orden;
END $$;
