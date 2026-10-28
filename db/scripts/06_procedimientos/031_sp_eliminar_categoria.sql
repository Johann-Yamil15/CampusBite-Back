-- JJ-Sprint4 27/10/2026: TEC-09. Elimina una categoría. Su encargado o admin_sistema.
-- Los productos de esa categoría quedan con id_categoria = NULL (ON DELETE SET NULL en la tabla producto).
--   CALL sp_eliminar_categoria(<id_categoria>, <id_actor>);
CREATE OR REPLACE PROCEDURE sp_eliminar_categoria(
  IN p_id_categoria UUID,
  IN p_id_actor     UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_cafeteria UUID;
BEGIN
  SELECT c.id_cafeteria INTO v_id_cafeteria
    FROM categoria c WHERE c.id_categoria = p_id_categoria FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'La categoría no existe' USING ERRCODE = 'no_data_found';
  END IF;

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'eliminar esta categoría');

  PERFORM set_config('app.id_actor', p_id_actor::text, true);

  DELETE FROM categoria WHERE id_categoria = p_id_categoria;
END $$;
