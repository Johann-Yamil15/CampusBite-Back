-- JY-Sprint2 10/10/2026: llena la bitácora general (02_tablas/013_bitacora.sql) con cada alta, cambio y baja.
-- Los argumentos del trigger son las columnas de la llave del registro.
-- Quién hizo el cambio llega en app.id_actor, el mismo valor que usa historial_estado_orden.
-- historial_estado_orden no se audita: ya es una bitácora.
CREATE OR REPLACE FUNCTION fn_bitacora() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_antes   JSONB;
  v_despues JSONB;
  v_fila    JSONB;
  v_id      TEXT := '';
  v_columna TEXT;
BEGIN
  -- contrasena_hash nunca se copia a la bitácora
  IF TG_OP <> 'INSERT' THEN v_antes   := to_jsonb(OLD) - 'contrasena_hash'; END IF;
  IF TG_OP <> 'DELETE' THEN v_despues := to_jsonb(NEW) - 'contrasena_hash'; END IF;

  -- Un UPDATE que no cambió nada (aparte de actualizado_en) no se registra
  IF TG_OP = 'UPDATE' AND (v_antes - 'actualizado_en') = (v_despues - 'actualizado_en') THEN
    RETURN NULL;
  END IF;

  v_fila := COALESCE(v_despues, v_antes);
  FOREACH v_columna IN ARRAY TG_ARGV LOOP
    v_id := v_id || CASE WHEN v_id = '' THEN '' ELSE ':' END || (v_fila ->> v_columna);
  END LOOP;

  INSERT INTO bitacora (tabla, id_registro, operacion, datos_antes, datos_despues, cambiado_por)
  VALUES (TG_TABLE_NAME, v_id, left(TG_OP, 1), v_antes, v_despues,
          NULLIF(current_setting('app.id_actor', true), '')::uuid);
  RETURN NULL;
END $$;

DO $$
DECLARE
  r RECORD;
BEGIN
  FOR r IN
    SELECT v.tabla, v.llave
      FROM (VALUES
        ('rol',                   '''id_rol'''),
        ('usuario',               '''id_usuario'''),
        ('cafeteria',             '''id_cafeteria'''),
        ('cafeteria_metodo_pago', '''id_cafeteria'', ''metodo'''),
        ('horario_cafeteria',     '''id_horario'''),
        ('categoria',             '''id_categoria'''),
        ('producto',              '''id_producto'''),
        ('orden',                 '''id_orden'''),
        ('detalle_orden',         '''id_detalle'''),
        ('pago',                  '''id_pago'''),
        ('suscripcion_push',      '''id_suscripcion''')
      ) AS v(tabla, llave)
  LOOP
    EXECUTE format('CREATE OR REPLACE TRIGGER trg_%s_bitacora AFTER INSERT OR UPDATE OR DELETE ON %I '
                   'FOR EACH ROW EXECUTE FUNCTION fn_bitacora(%s)', r.tabla, r.tabla, r.llave);
  END LOOP;
END $$;
