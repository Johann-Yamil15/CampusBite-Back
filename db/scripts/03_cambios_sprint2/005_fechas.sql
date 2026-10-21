-- JY-Sprint2 09/10/2026: revisión de fechas.
-- Convención: TIMESTAMPTZ (instante, se guarda en UTC) termina en _en; TIME (hora local) empieza con hora_;
-- DATE (día sin hora) empieza con fecha_.
--  1. Nombres: orden.creada_en y suscripcion_push.creada_en -> creado_en (igual que el resto de tablas).
--     orden.hora_recogida no es una hora sino un instante completo -> recogida_en.
--  2. orden.actualizado_en era redundante: en una orden solo cambia el estado, y cada cambio ya queda con
--     su fecha en historial_estado_orden. Se borra (con su trigger).
--  3. No se agrega usuario.fecha_registro: sería creado_en::date guardado dos veces. Si hace falta el día,
--     se calcula: (creado_en AT TIME ZONE 'America/Mexico_City')::date.
--  4. Reglas de orden entre fechas que antes no existían (CHECK).
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'orden' AND column_name = 'creada_en') THEN
    ALTER TABLE orden RENAME COLUMN creada_en TO creado_en;
  END IF;
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'suscripcion_push' AND column_name = 'creada_en') THEN
    ALTER TABLE suscripcion_push RENAME COLUMN creada_en TO creado_en;
  END IF;
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'orden' AND column_name = 'hora_recogida') THEN
    ALTER TABLE orden RENAME COLUMN hora_recogida TO recogida_en;
  END IF;
END $$;

DROP TRIGGER IF EXISTS trg_orden_upd ON orden;
ALTER TABLE orden DROP COLUMN IF EXISTS actualizado_en;

DO $$
DECLARE
  r RECORD;
BEGIN
  FOR r IN
    SELECT v.tabla, v.nombre, v.regla
      FROM (VALUES
        ('usuario',   'ck_usuario_fechas',   'actualizado_en >= creado_en'),
        ('cafeteria', 'ck_cafeteria_fechas', 'actualizado_en >= creado_en'),
        ('producto',  'ck_producto_fechas',  'actualizado_en >= creado_en'),
        ('orden',     'ck_orden_recogida',   'recogida_en > creado_en'),
        ('pago',      'ck_pago_fechas',      'pagado_en <= actualizado_en')
      ) AS v(tabla, nombre, regla)
  LOOP
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conrelid = r.tabla::regclass AND conname = r.nombre) THEN
      EXECUTE format('ALTER TABLE %I ADD CONSTRAINT %I CHECK (%s)', r.tabla, r.nombre, r.regla);
    END IF;
  END LOOP;
END $$;

COMMENT ON COLUMN orden.recogida_en IS 'Instante en que el alumno recoge la orden (TIMESTAMPTZ, UTC).';
COMMENT ON COLUMN horario_cafeteria.hora_apertura IS 'Hora local del campus (America/Mexico_City).';
COMMENT ON COLUMN horario_cafeteria.hora_cierre   IS 'Hora local del campus (America/Mexico_City).';
