-- JJ-Sprint4 27/10/2026: TEC-09. Crea una categoría en la cafetería. Su encargado o admin_sistema.
--   CALL sp_crear_categoria(<id_cafeteria>, <id_actor>, 'Bebidas', NULL);
-- Nombre repetido en la cafetería -> 23505.
CREATE OR REPLACE PROCEDURE sp_crear_categoria(
  IN  p_id_cafeteria UUID,
  IN  p_id_actor     UUID,
  IN  p_nombre       VARCHAR,
  OUT p_id_categoria UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'crear categorías en esta cafetería');

  PERFORM set_config('app.id_actor', p_id_actor::text, true);

  INSERT INTO categoria (id_cafeteria, nombre)
  VALUES (p_id_cafeteria, btrim(p_nombre))
  RETURNING id_categoria INTO p_id_categoria;
EXCEPTION WHEN unique_violation THEN
  RAISE EXCEPTION 'Ya existe una categoría con ese nombre en esta cafetería'
    USING ERRCODE = 'unique_violation';
END $$;
