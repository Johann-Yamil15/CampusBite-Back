-- JJ-Sprint4 27/10/2026: TEC-09. Actualiza un producto existente. Su encargado o admin_sistema.
--   CALL sp_actualizar_producto(<id_producto>, <id_actor>, <id_categoria o NULL>,
--                               'Nuevo nombre', 'Nueva descripción', 55.00, 'https://...', true);
-- Nombre repetido en la cafetería -> 23505. Producto inexistente -> P0002. Permiso -> 42501.
CREATE OR REPLACE PROCEDURE sp_actualizar_producto(
  IN p_id_producto  UUID,
  IN p_id_actor     UUID,
  IN p_id_categoria UUID,
  IN p_nombre       VARCHAR,
  IN p_descripcion  VARCHAR,
  IN p_precio       NUMERIC,
  IN p_disponible   BOOLEAN
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

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'editar este producto');
  CALL sp_validar_datos_producto(v_id_cafeteria, p_id_categoria, p_nombre, p_descripcion, p_precio, NULL);

  PERFORM set_config('app.id_actor', p_id_actor::text, true);

  UPDATE producto
     SET id_categoria = p_id_categoria,
         nombre       = btrim(p_nombre),
         descripcion  = NULLIF(btrim(p_descripcion), ''),
         precio       = p_precio,
         disponible   = COALESCE(p_disponible, disponible)
   WHERE id_producto = p_id_producto;
EXCEPTION WHEN unique_violation THEN
  RAISE EXCEPTION 'Ya existe un producto con el nombre "%" en esta cafetería', btrim(p_nombre)
    USING ERRCODE = 'unique_violation';
END $$;
