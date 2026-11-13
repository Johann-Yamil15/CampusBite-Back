-- JJ-Sprint5 13/11/2026: horario semanal y "cerrar por hoy" (solo su encargado o admin_sistema).
-- El front edita UN tramo por día: si hay varios en horario_cafeteria se devuelve desde = mínimo, hasta = máximo.
-- p_horario: {"dias": [7 x {"abre", "desde": "HH:MI", "hasta": "HH:MI"}] (lunes = índice 0), "cerrado_hoy", "abierta_ahora"}
CREATE OR REPLACE PROCEDURE sp_obtener_horario(
  IN  p_id_cafeteria UUID,
  IN  p_id_actor     UUID,
  OUT p_horario      JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM cafeteria c WHERE c.id_cafeteria = p_id_cafeteria) THEN
    RAISE EXCEPTION 'La cafetería no existe' USING ERRCODE = 'no_data_found';
  END IF;
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'ver el horario de esta cafetería');

  SELECT jsonb_build_object(
           'dias', jsonb_agg(jsonb_build_object(
                     'abre',  h.desde IS NOT NULL,
                     'desde', COALESCE(to_char(h.desde, 'HH24:MI'), '09:00'),
                     'hasta', COALESCE(to_char(h.hasta, 'HH24:MI'), '17:00')) ORDER BY d.dia),
           'cerrado_hoy', EXISTS (SELECT 1 FROM cierre_cafeteria k
                                   WHERE k.id_cafeteria = p_id_cafeteria
                                     AND k.fecha = (now() AT TIME ZONE 'America/Mexico_City')::date),
           'abierta_ahora', fn_cafeteria_abierta(p_id_cafeteria))
    INTO p_horario
    FROM generate_series(1, 7) d(dia)
    LEFT JOIN LATERAL (SELECT min(x.hora_apertura) AS desde, max(x.hora_cierre) AS hasta
                         FROM horario_cafeteria x
                        WHERE x.id_cafeteria = p_id_cafeteria AND x.dia_semana = d.dia
                       HAVING count(*) > 0) h ON true;
END $$;

-- p_dias: [7 x {"abre", "desde", "hasta"}], lunes primero. Reemplaza la semana completa.
CREATE OR REPLACE PROCEDURE sp_guardar_horario(
  IN p_id_cafeteria UUID,
  IN p_id_actor     UUID,
  IN p_dias         JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_nombres CONSTANT TEXT[] := ARRAY['Lunes','Martes','Miércoles','Jueves','Viernes','Sábado','Domingo'];
  v_dia     JSONB;
  v_i       INT;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM cafeteria c WHERE c.id_cafeteria = p_id_cafeteria) THEN
    RAISE EXCEPTION 'La cafetería no existe' USING ERRCODE = 'no_data_found';
  END IF;
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'cambiar el horario de esta cafetería');

  IF p_dias IS NULL OR jsonb_typeof(p_dias) <> 'array' THEN
    RAISE EXCEPTION 'El horario debe tener exactamente 7 días';
  END IF;
  IF jsonb_array_length(p_dias) <> 7 THEN
    RAISE EXCEPTION 'El horario debe tener exactamente 7 días';
  END IF;

  DELETE FROM horario_cafeteria WHERE id_cafeteria = p_id_cafeteria;
  FOR v_i IN 1..7 LOOP
    v_dia := p_dias -> (v_i - 1);
    CONTINUE WHEN NOT COALESCE((v_dia ->> 'abre')::boolean, false);
    IF (v_dia ->> 'hasta')::time <= (v_dia ->> 'desde')::time THEN
      RAISE EXCEPTION '%: la hora de cierre tiene que ser después de la de apertura', v_nombres[v_i];
    END IF;
    INSERT INTO horario_cafeteria (id_cafeteria, dia_semana, hora_apertura, hora_cierre)
    VALUES (p_id_cafeteria, v_i, (v_dia ->> 'desde')::time, (v_dia ->> 'hasta')::time);
  END LOOP;
END $$;

-- p_cerrar = true cierra solo por hoy; false vuelve a abrir. Idempotente. Los pedidos aceptados no se cancelan.
CREATE OR REPLACE PROCEDURE sp_cambiar_cierre_hoy(
  IN p_id_cafeteria UUID,
  IN p_id_actor     UUID,
  IN p_cerrar       BOOLEAN
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_hoy CONSTANT DATE := (now() AT TIME ZONE 'America/Mexico_City')::date;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM cafeteria c WHERE c.id_cafeteria = p_id_cafeteria) THEN
    RAISE EXCEPTION 'La cafetería no existe' USING ERRCODE = 'no_data_found';
  END IF;
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'cerrar o abrir esta cafetería');

  IF p_cerrar THEN
    INSERT INTO cierre_cafeteria (id_cafeteria, fecha, creado_por) VALUES (p_id_cafeteria, v_hoy, p_id_actor)
    ON CONFLICT DO NOTHING;
  ELSE
    DELETE FROM cierre_cafeteria WHERE id_cafeteria = p_id_cafeteria AND fecha = v_hoy;
  END IF;
END $$;
