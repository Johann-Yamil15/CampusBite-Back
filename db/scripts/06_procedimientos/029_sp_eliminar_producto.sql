-- JJ-Sprint4 27/10/2026: TEC-09. Elimina un producto físicamente. Su encargado o admin_sistema.
-- Si tiene órdenes activas (pendiente/confirmada/en_preparacion/lista) devuelve 55006 (object_in_use):
-- el encargado debe desactivarlo en su lugar con sp_toggle_disponible_producto.
--   CALL sp_eliminar_producto(<id_producto>, <id_actor>);
CREATE OR REPLACE PROCEDURE sp_eliminar_producto(
  IN p_id_producto UUID,
  IN p_id_actor    UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_cafeteria UUID;
  v_en_ordenes   INT;
BEGIN
  SELECT p.id_cafeteria INTO v_id_cafeteria
    FROM producto p WHERE p.id_producto = p_id_producto FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'El producto no existe' USING ERRCODE = 'no_data_found';
  END IF;

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'eliminar este producto');

  SELECT count(*) INTO v_en_ordenes
    FROM detalle_orden d
    JOIN orden o ON o.id_orden = d.id_orden
   WHERE d.id_producto = p_id_producto
     AND o.estado IN ('pendiente', 'confirmada', 'en_preparacion', 'lista');

  IF v_en_ordenes > 0 THEN
    RAISE EXCEPTION 'El producto tiene % orden(es) activa(s); desactívalo en su lugar', v_en_ordenes
      USING ERRCODE = 'object_in_use';
  END IF;

  PERFORM set_config('app.id_actor', p_id_actor::text, true);

  DELETE FROM producto WHERE id_producto = p_id_producto;
END $$;
