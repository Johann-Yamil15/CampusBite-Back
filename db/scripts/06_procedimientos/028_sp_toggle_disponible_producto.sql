-- JJ-Sprint4 27/10/2026: TEC-09. Cambia solo la disponibilidad de un producto (toggle rápido del panel admin).
-- Su encargado o admin_sistema.
--   CALL sp_toggle_disponible_producto(<id_producto>, false, <id_actor>);
CREATE OR REPLACE PROCEDURE sp_toggle_disponible_producto(
  IN p_id_producto UUID,
  IN p_disponible  BOOLEAN,
  IN p_id_actor    UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_cafeteria UUID;
BEGIN
  SELECT p.id_cafeteria INTO v_id_cafeteria
    FROM producto p WHERE p.id_producto = p_id_producto FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'El producto no existe' USING ERRCODE = 'no_data_found';
  END IF;

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'cambiar disponibilidad de este producto');

  PERFORM set_config('app.id_actor', p_id_actor::text, true);

  UPDATE producto SET disponible = p_disponible WHERE id_producto = p_id_producto;
END $$;
