-- JY-Sprint3 20/10/2026: interno. Reglas de los datos de una cafetería que no cubren los CHECK de la tabla.
-- Lo usan sp_crear_cafeteria y sp_actualizar_cafeteria para que las dos validen exactamente igual.
-- La API ya valida lo mismo; esto es la segunda barrera por si alguien llama al procedimiento directo.
CREATE OR REPLACE PROCEDURE sp_validar_datos_cafeteria(
  IN p_nombre        VARCHAR,
  IN p_ubicacion     VARCHAR,
  IN p_punto_entrega VARCHAR,
  IN p_es_del_campus BOOLEAN,
  IN p_metodos       metodo_pago[]
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF length(btrim(COALESCE(p_nombre, ''))) < 2 THEN
    RAISE EXCEPTION 'El nombre debe tener al menos 2 caracteres' USING ERRCODE = 'check_violation';
  END IF;
  IF btrim(COALESCE(p_ubicacion, '')) = '' THEN
    RAISE EXCEPTION 'La ubicación es obligatoria' USING ERRCODE = 'check_violation';
  END IF;
  IF btrim(COALESCE(p_punto_entrega, '')) = '' THEN
    RAISE EXCEPTION 'El punto de entrega es obligatorio' USING ERRCODE = 'check_violation';
  END IF;
  IF p_es_del_campus IS NULL THEN
    RAISE EXCEPTION 'Indica si la cafetería es del campus' USING ERRCODE = 'check_violation';
  END IF;
  IF COALESCE(cardinality(array_remove(p_metodos, NULL)), 0) = 0 THEN
    RAISE EXCEPTION 'La cafetería debe aceptar al menos un método de pago' USING ERRCODE = 'check_violation';
  END IF;
END $$;
