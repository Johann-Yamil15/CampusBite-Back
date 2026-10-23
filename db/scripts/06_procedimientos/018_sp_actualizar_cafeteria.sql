-- JY-Sprint3 20/10/2026: TEC-08. Edita los datos y reemplaza los métodos de pago. Su encargado o admin_sistema.
--   CALL sp_actualizar_cafeteria(<id_cafeteria>, <id_actor>, 'Cafetería Norte', 'Edificio K', true,
--                                'Ventanilla 3', ARRAY['efectivo']::metodo_pago[]);
-- No cambia "activa": eso es sp_cambiar_activa_cafeteria (solo admin_sistema).
CREATE OR REPLACE PROCEDURE sp_actualizar_cafeteria(
  IN p_id_cafeteria  UUID,
  IN p_id_actor      UUID,
  IN p_nombre        VARCHAR,
  IN p_ubicacion     VARCHAR,
  IN p_es_del_campus BOOLEAN,
  IN p_punto_entrega VARCHAR,
  IN p_metodos       metodo_pago[]
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  -- Se bloquea la fila: dos ediciones simultáneas se forman en fila en lugar de mezclar métodos de pago
  PERFORM 1 FROM cafeteria c WHERE c.id_cafeteria = p_id_cafeteria FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'La cafetería no existe' USING ERRCODE = 'no_data_found';
  END IF;

  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'editar esta cafetería');
  CALL sp_validar_datos_cafeteria(p_nombre, p_ubicacion, p_punto_entrega, p_es_del_campus, p_metodos);

  PERFORM set_config('app.id_actor', p_id_actor::text, true);

  UPDATE cafeteria
     SET nombre = btrim(p_nombre), ubicacion = btrim(p_ubicacion),
         es_del_campus = p_es_del_campus, punto_entrega = btrim(p_punto_entrega)
   WHERE id_cafeteria = p_id_cafeteria;

  -- Solo se tocan los métodos que cambian, para que la bitácora muestre altas y bajas reales
  DELETE FROM cafeteria_metodo_pago m
   WHERE m.id_cafeteria = p_id_cafeteria AND m.metodo <> ALL (array_remove(p_metodos, NULL));
  INSERT INTO cafeteria_metodo_pago (id_cafeteria, metodo)
  SELECT DISTINCT p_id_cafeteria, m FROM unnest(p_metodos) m WHERE m IS NOT NULL
  ON CONFLICT (id_cafeteria, metodo) DO NOTHING;
EXCEPTION WHEN unique_violation THEN
  RAISE EXCEPTION 'Ya existe una cafetería con el nombre "%"', btrim(p_nombre) USING ERRCODE = 'unique_violation';
END $$;
