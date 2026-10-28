-- JY-Sprint4 27/10/2026: TEC-09. Alta de un producto en el menú de una cafetería. Su encargado o admin_sistema.
--   CALL sp_crear_producto(<id_producto>, <id_cafeteria>, <id_actor>, <id_categoria o NULL>, 'Chilaquiles verdes',
--                          'Con pollo y crema', 55.00, true, 'https://.../productos/<id_producto>_<fecha>.webp');
-- p_id_producto lo genera la API: la imagen se sube a Azure antes del alta y su nombre lleva ese id.
-- p_imagen_url puede ser NULL (producto sin foto). Nombre repetido en la cafetería (sin distinguir mayúsculas) -> 23505.
-- Solo guarda, igual que los demás sp_crear_*: la API ya tiene el id y lee el producto con sp_obtener_producto.
-- La primera versión devolvía el producto como JSONB (OUT p_producto); se quita esa firma.
DROP PROCEDURE IF EXISTS sp_crear_producto(UUID, UUID, UUID, UUID, VARCHAR, VARCHAR, NUMERIC, BOOLEAN, VARCHAR, JSONB);

CREATE OR REPLACE PROCEDURE sp_crear_producto(
  IN  p_id_producto  UUID,
  IN  p_id_cafeteria UUID,
  IN  p_id_actor     UUID,
  IN  p_id_categoria UUID,
  IN  p_nombre       VARCHAR,
  IN  p_descripcion  VARCHAR,
  IN  p_precio       NUMERIC,
  IN  p_disponible   BOOLEAN,
  IN  p_imagen_url   VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF p_id_producto IS NULL THEN
    RAISE EXCEPTION 'Falta el id del producto' USING ERRCODE = 'check_violation';
  END IF;
  PERFORM 1 FROM cafeteria c WHERE c.id_cafeteria = p_id_cafeteria;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'La cafetería no existe' USING ERRCODE = 'no_data_found';
  END IF;

  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'agregar productos a esta cafetería');
  CALL sp_validar_datos_producto(p_id_cafeteria, p_id_categoria, p_nombre, p_descripcion, p_precio, p_imagen_url);

  PERFORM set_config('app.id_actor', p_id_actor::text, true);      -- autor en la bitácora

  INSERT INTO producto (id_producto, id_cafeteria, id_categoria, nombre, descripcion, precio, imagen_url, disponible)
  VALUES (p_id_producto, p_id_cafeteria, p_id_categoria, btrim(p_nombre), NULLIF(btrim(p_descripcion), ''),
          p_precio, p_imagen_url, COALESCE(p_disponible, true));
EXCEPTION WHEN unique_violation THEN
  RAISE EXCEPTION 'Ya existe un producto con el nombre "%" en esta cafetería', btrim(p_nombre) USING ERRCODE = 'unique_violation';
END $$;
