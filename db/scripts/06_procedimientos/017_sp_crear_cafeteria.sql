-- JY-Sprint3 20/10/2026: TEC-08. Alta de cafetería con sus métodos de pago, en una transacción. Solo admin_sistema.
--   CALL sp_crear_cafeteria(<id_admin>, 'Cafetería Norte', 'Edificio K', true, 'Ventanilla 2',
--                           ARRAY['efectivo','tarjeta']::metodo_pago[], NULL);
-- Nombre repetido (sin distinguir mayúsculas) -> 23505.
CREATE OR REPLACE PROCEDURE sp_crear_cafeteria(
  IN  p_id_actor      UUID,
  IN  p_nombre        VARCHAR,
  IN  p_ubicacion     VARCHAR,
  IN  p_es_del_campus BOOLEAN,
  IN  p_punto_entrega VARCHAR,
  IN  p_metodos       metodo_pago[],
  OUT p_id_cafeteria  UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  CALL sp_exigir_admin_sistema(p_id_actor, 'crear cafeterías');
  CALL sp_validar_datos_cafeteria(p_nombre, p_ubicacion, p_punto_entrega, p_es_del_campus, p_metodos);

  PERFORM set_config('app.id_actor', p_id_actor::text, true);      -- autor en la bitácora

  INSERT INTO cafeteria (nombre, ubicacion, es_del_campus, punto_entrega)
  VALUES (btrim(p_nombre), btrim(p_ubicacion), p_es_del_campus, btrim(p_punto_entrega))
  RETURNING id_cafeteria INTO p_id_cafeteria;

  INSERT INTO cafeteria_metodo_pago (id_cafeteria, metodo)
  SELECT DISTINCT p_id_cafeteria, m FROM unnest(p_metodos) m WHERE m IS NOT NULL;
EXCEPTION WHEN unique_violation THEN
  RAISE EXCEPTION 'Ya existe una cafetería con el nombre "%"', btrim(p_nombre) USING ERRCODE = 'unique_violation';
END $$;
