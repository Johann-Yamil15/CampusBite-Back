-- JY-Sprint2 09/10/2026: entrega con el código del alumno; el efectivo se cobra aquí.
--   CALL sp_entregar_orden(<id_orden>, '123456', <id_encargado>);
CREATE OR REPLACE PROCEDURE sp_entregar_orden(
  IN p_id_orden UUID,
  IN p_codigo   VARCHAR,
  IN p_id_actor UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_cafeteria UUID;
  v_estado       estado_orden;
  v_codigo       CHAR(6);
BEGIN
  SELECT o.id_cafeteria, o.estado, o.codigo_recogida INTO v_id_cafeteria, v_estado, v_codigo
    FROM orden o WHERE o.id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'entregar esta orden');

  IF v_estado <> 'lista' THEN RAISE EXCEPTION 'La orden no está lista para entrega'; END IF;
  IF v_codigo <> p_codigo THEN RAISE EXCEPTION 'Código de recogida incorrecto'; END IF;

  UPDATE pago SET estado = 'pagado', pagado_en = now()
   WHERE id_orden = p_id_orden AND metodo = 'efectivo' AND estado = 'pendiente';
  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE orden SET estado = 'entregada' WHERE id_orden = p_id_orden;
END $$;
