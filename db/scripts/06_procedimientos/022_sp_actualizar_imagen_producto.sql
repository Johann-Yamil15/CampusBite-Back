-- JY-Sprint4 27/10/2026: TEC-10. Guarda la URL de la imagen de un producto (la API ya la subió a Azure Blob Storage).
--   CALL sp_actualizar_imagen_producto(<id_cafeteria>, <id_producto>, <id_actor>,
--                                      'https://campusbite.blob.core.windows.net/productos/<id>_<fecha>.webp', NULL);
-- p_imagen_anterior: la URL que tenía antes (o NULL), para que la API borre ese blob después de guardar la nueva.
-- Su encargado o admin_sistema. Si el producto no es de esa cafetería se responde "no existe" (no se revela).
-- Va aparte de sp_actualizar_producto (TEC-09): la imagen se cambia sola, sin reenviar nombre, precio, etc.
CREATE OR REPLACE PROCEDURE sp_actualizar_imagen_producto(
  IN  p_id_cafeteria    UUID,
  IN  p_id_producto     UUID,
  IN  p_id_actor        UUID,
  IN  p_imagen_url      VARCHAR,
  OUT p_imagen_anterior VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  -- http:// solo para el emulador local (Azurite); en Azure siempre es https://
  IF p_imagen_url IS NULL OR p_imagen_url !~ '^https?://[^[:space:]]+$' OR length(p_imagen_url) > 255 THEN
    RAISE EXCEPTION 'La URL de la imagen no es válida' USING ERRCODE = 'check_violation';
  END IF;

  -- Se bloquea la fila: dos subidas simultáneas se forman en fila y cada una recibe la URL que reemplazó,
  -- así ningún blob se queda sin borrar ni se borra el que está en uso
  SELECT p.imagen_url INTO p_imagen_anterior
    FROM producto p
   WHERE p.id_producto = p_id_producto AND p.id_cafeteria = p_id_cafeteria
     FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'El producto no existe' USING ERRCODE = 'no_data_found';
  END IF;

  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'cambiar la imagen de este producto');

  PERFORM set_config('app.id_actor', p_id_actor::text, true);

  UPDATE producto SET imagen_url = p_imagen_url WHERE id_producto = p_id_producto;
END $$;
