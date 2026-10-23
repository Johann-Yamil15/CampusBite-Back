-- JJ-Sprint3 22/10/2026: TEC-11. Autorregistro público de un negocio: crea al encargado (admin_cafeteria) y su
-- cafetería en una sola transacción, y los enlaza. Coexiste con sp_crear_cafeteria (alta por admin_sistema, sin
-- encargado); por eso NO recibe p_id_actor ni llama sp_exigir_admin_sistema: es público, como sp_registrar_usuario.
--   CALL sp_autorregistrar_cafeteria('Ana López', 'ana@negocio.mx', '<hash bcrypt>', NULL,
--                                    'Cafetería Norte', 'Edificio K', true, 'Ventanilla 2',
--                                    ARRAY['efectivo','tarjeta']::metodo_pago[], NULL);
-- Correo o nombre de cafetería repetidos -> 23505 con un mensaje distinto para cada caso.
-- Orden: usuario -> cafetería -> enlace (usuario.id_cafeteria). Así ck_usuario_cafeteria_solo_encargado nunca se
-- viola a medio camino, y si algo falla no queda ni usuario ni cafetería huérfanos (todo el CALL es atómico).
CREATE OR REPLACE PROCEDURE sp_autorregistrar_cafeteria(
  IN  p_nombre_encargado  VARCHAR,
  IN  p_correo            VARCHAR,
  IN  p_contrasena_hash   VARCHAR,
  IN  p_telefono          VARCHAR,
  IN  p_nombre_cafeteria  VARCHAR,
  IN  p_ubicacion         VARCHAR,
  IN  p_es_del_campus     BOOLEAN,
  IN  p_punto_entrega     VARCHAR,
  IN  p_metodos           metodo_pago[],
  OUT p_id_usuario        UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_cafeteria UUID;
BEGIN
  CALL sp_validar_datos_cafeteria(p_nombre_cafeteria, p_ubicacion, p_punto_entrega, p_es_del_campus, p_metodos);

  BEGIN
    INSERT INTO usuario (id_rol, nombre, correo, contrasena_hash, telefono)
    VALUES (2, btrim(p_nombre_encargado), p_correo, p_contrasena_hash, p_telefono)
    RETURNING id_usuario INTO p_id_usuario;
  EXCEPTION WHEN unique_violation THEN
    RAISE EXCEPTION 'El correo ya está registrado' USING ERRCODE = 'unique_violation';
  END;

  PERFORM set_config('app.id_actor', p_id_usuario::text, true);  -- autor en la bitácora: el propio encargado

  BEGIN
    INSERT INTO cafeteria (nombre, ubicacion, es_del_campus, punto_entrega)
    VALUES (btrim(p_nombre_cafeteria), btrim(p_ubicacion), p_es_del_campus, btrim(p_punto_entrega))
    RETURNING id_cafeteria INTO v_id_cafeteria;
  EXCEPTION WHEN unique_violation THEN
    RAISE EXCEPTION 'Ya existe una cafetería con el nombre "%"', btrim(p_nombre_cafeteria) USING ERRCODE = 'unique_violation';
  END;

  INSERT INTO cafeteria_metodo_pago (id_cafeteria, metodo)
  SELECT DISTINCT v_id_cafeteria, m FROM unnest(p_metodos) m WHERE m IS NOT NULL;

  UPDATE usuario SET id_cafeteria = v_id_cafeteria WHERE id_usuario = p_id_usuario;
END $$;
