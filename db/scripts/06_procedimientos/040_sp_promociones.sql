-- JJ-Sprint5 13/11/2026: promociones de una cafetería (CRUD del encargado) y las activas para el alumno.
-- Por ahora son informativas: usos = 0 hasta que se liguen a órdenes.
CREATE OR REPLACE FUNCTION fn_promocion_json(p_id_promocion UUID)
RETURNS JSONB LANGUAGE sql STABLE SET search_path = public, pg_temp AS $$
  SELECT jsonb_build_object('id', p.id_promocion, 'nombre', p.nombre, 'descripcion', p.descripcion, 'tipo', p.tipo,
                            'valor', p.valor, 'desde', p.desde, 'hasta', p.hasta, 'activa', p.activa, 'usos', 0)
    FROM promocion p WHERE p.id_promocion = p_id_promocion;
$$;

CREATE OR REPLACE PROCEDURE sp_listar_promociones(IN p_id_cafeteria UUID, IN p_id_actor UUID, OUT p_lista JSONB)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'ver las promociones de esta cafetería');
  SELECT COALESCE(jsonb_agg(fn_promocion_json(p.id_promocion) ORDER BY p.desde DESC, p.nombre), '[]'::jsonb)
    INTO p_lista
    FROM promocion p WHERE p.id_cafeteria = p_id_cafeteria;
END $$;

-- p_id_promocion NULL = crear. Devuelve la promoción guardada.
CREATE OR REPLACE PROCEDURE sp_guardar_promocion(
  IN  p_id_cafeteria UUID,
  IN  p_id_actor     UUID,
  IN  p_id_promocion UUID,
  IN  p_nombre       VARCHAR,
  IN  p_descripcion  VARCHAR,
  IN  p_tipo         tipo_promocion,
  IN  p_valor        NUMERIC,
  IN  p_desde        DATE,
  IN  p_hasta        DATE,
  IN  p_activa       BOOLEAN,
  OUT p_promocion    JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id UUID;
BEGIN
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'cambiar las promociones de esta cafetería');

  IF p_hasta < p_desde THEN
    RAISE EXCEPTION 'La fecha final no puede ser anterior a la inicial';
  END IF;
  IF p_tipo = 'porcentaje' AND (p_valor NOT BETWEEN 1 AND 90 OR p_valor <> trunc(p_valor)) THEN
    RAISE EXCEPTION 'El porcentaje debe ser un entero entre 1 y 90';
  END IF;
  IF p_tipo <> 'porcentaje' AND (p_valor <= 0 OR p_valor > 2000) THEN
    RAISE EXCEPTION 'El precio debe ser mayor a 0 y máximo 2000';
  END IF;

  IF p_id_promocion IS NULL THEN
    INSERT INTO promocion (id_cafeteria, nombre, descripcion, tipo, valor, desde, hasta, activa)
    VALUES (p_id_cafeteria, btrim(p_nombre), COALESCE(btrim(p_descripcion), ''), p_tipo, p_valor, p_desde, p_hasta, p_activa)
    RETURNING id_promocion INTO v_id;
  ELSE
    UPDATE promocion
       SET nombre = btrim(p_nombre), descripcion = COALESCE(btrim(p_descripcion), ''), tipo = p_tipo,
           valor = p_valor, desde = p_desde, hasta = p_hasta, activa = p_activa
     WHERE id_promocion = p_id_promocion AND id_cafeteria = p_id_cafeteria
    RETURNING id_promocion INTO v_id;
    IF v_id IS NULL THEN RAISE EXCEPTION 'La promoción no existe' USING ERRCODE = 'no_data_found'; END IF;
  END IF;

  p_promocion := fn_promocion_json(v_id);
END $$;

CREATE OR REPLACE PROCEDURE sp_eliminar_promocion(IN p_id_cafeteria UUID, IN p_id_actor UUID, IN p_id_promocion UUID)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'cambiar las promociones de esta cafetería');
  DELETE FROM promocion WHERE id_promocion = p_id_promocion AND id_cafeteria = p_id_cafeteria;
  IF NOT FOUND THEN RAISE EXCEPTION 'La promoción no existe' USING ERRCODE = 'no_data_found'; END IF;
END $$;

-- Carrusel del alumno: activas, vigentes hoy (hora de México) y de una cafetería activa (o de la plataforma).
-- etiqueta: "-15%" | "A $45" | "Combo $79"
CREATE OR REPLACE PROCEDURE sp_promociones_activas(OUT p_lista JSONB)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_hoy CONSTANT DATE := (now() AT TIME ZONE 'America/Mexico_City')::date;
BEGIN
  SELECT COALESCE(jsonb_agg(jsonb_build_object(
           'id_promocion', p.id_promocion,
           'etiqueta',     CASE p.tipo
                             WHEN 'porcentaje'  THEN '-' || p.valor::int || '%'
                             WHEN 'precio_fijo' THEN 'A $' || rtrim(to_char(p.valor, 'FM999990.99'), '.')
                             ELSE 'Combo $' || rtrim(to_char(p.valor, 'FM999990.99'), '.') END,
           'titulo',       p.nombre,
           'subtitulo',    concat_ws(' · ', NULLIF(p.descripcion, ''), c.nombre),
           'imagen_url',   c.portada_url,
           'id_cafeteria', p.id_cafeteria,
           'categoria',    p.categoria) ORDER BY p.hasta, p.nombre), '[]'::jsonb)
    INTO p_lista
    FROM promocion p
    LEFT JOIN cafeteria c ON c.id_cafeteria = p.id_cafeteria
   WHERE p.activa AND v_hoy BETWEEN p.desde AND p.hasta
     AND (p.id_cafeteria IS NULL OR c.activa);
END $$;
