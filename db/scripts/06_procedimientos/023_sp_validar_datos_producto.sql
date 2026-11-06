-- JY-Sprint4 27/10/2026: TEC-09. Interno. Reglas de los datos de un producto que no cubren los CHECK de la tabla.
-- Lo usa sp_crear_producto (y lo usará sp_actualizar_producto) para que las dos validen exactamente igual.
-- La API ya valida lo mismo; esto es la segunda barrera por si alguien llama al procedimiento directo.
CREATE OR REPLACE PROCEDURE sp_validar_datos_producto(
  IN p_id_cafeteria UUID,
  IN p_id_categoria UUID,
  IN p_nombre       VARCHAR,
  IN p_descripcion  VARCHAR,
  IN p_precio       NUMERIC,
  IN p_imagen_url   VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF length(btrim(COALESCE(p_nombre, ''))) NOT BETWEEN 2 AND 100 THEN
    RAISE EXCEPTION 'El nombre debe tener entre 2 y 100 caracteres' USING ERRCODE = 'check_violation';
  END IF;
  IF length(btrim(p_descripcion)) > 500 THEN
    RAISE EXCEPTION 'La descripción puede tener máximo 500 caracteres' USING ERRCODE = 'check_violation';
  END IF;
  -- precio es NUMERIC(8,2): máximo 999999.99 y sin más de 2 decimales (no se redondea a escondidas)
  IF p_precio IS NULL OR p_precio <= 0 OR p_precio > 999999.99 OR p_precio <> round(p_precio, 2) THEN
    RAISE EXCEPTION 'El precio debe ser mayor a 0, menor a 1,000,000 y con máximo 2 decimales' USING ERRCODE = 'check_violation';
  END IF;
  -- Misma regla que sp_actualizar_imagen_producto; http:// solo para el emulador local (Azurite)
  IF p_imagen_url IS NOT NULL AND (p_imagen_url !~ '^https?://[^[:space:]]+$' OR length(p_imagen_url) > 255) THEN
    RAISE EXCEPTION 'La URL de la imagen no es válida' USING ERRCODE = 'check_violation';
  END IF;
  IF p_id_categoria IS NOT NULL AND NOT EXISTS (
    SELECT 1 FROM categoria c WHERE c.id_categoria = p_id_categoria AND c.id_cafeteria = p_id_cafeteria
  ) THEN
    RAISE EXCEPTION 'La categoría no existe en esta cafetería' USING ERRCODE = 'check_violation';
  END IF;
END $$;
