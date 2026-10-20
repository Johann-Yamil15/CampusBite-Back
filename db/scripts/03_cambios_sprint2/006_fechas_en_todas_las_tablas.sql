-- JY-Sprint2 10/10/2026: toda tabla sabe cuándo se creó cada fila (creado_en) y, si la fila se puede editar,
-- cuándo cambió por última vez (actualizado_en). El detalle de CADA cambio (qué, quién y cuándo) está en la
-- tabla bitacora (02_tablas/013_bitacora.sql y 04_triggers/008_bitacora.sql).
--
-- Excepciones, a propósito:
--  * historial_estado_orden ya tiene cambiado_en, que es su fecha de creación (y sus filas no se editan).
--  * orden no lleva actualizado_en: en una orden solo cambia el estado, y cada cambio queda con fecha y autor en
--    historial_estado_orden (ver 005_fechas.sql).
--  * cafeteria_metodo_pago, detalle_orden y suscripcion_push solo se insertan o borran: basta creado_en.
ALTER TABLE rol                   ADD COLUMN IF NOT EXISTS creado_en      TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE rol                   ADD COLUMN IF NOT EXISTS actualizado_en TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE categoria             ADD COLUMN IF NOT EXISTS creado_en      TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE categoria             ADD COLUMN IF NOT EXISTS actualizado_en TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE horario_cafeteria     ADD COLUMN IF NOT EXISTS creado_en      TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE horario_cafeteria     ADD COLUMN IF NOT EXISTS actualizado_en TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE cafeteria_metodo_pago ADD COLUMN IF NOT EXISTS creado_en      TIMESTAMPTZ NOT NULL DEFAULT now();

-- En detalle_orden y pago la fecha real se conoce: es la de su orden (se crean en la misma transacción).
-- Las filas que ya existían toman esa fecha en lugar de la fecha de esta migración.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                  WHERE table_schema = 'public' AND table_name = 'detalle_orden' AND column_name = 'creado_en') THEN
    ALTER TABLE detalle_orden ADD COLUMN creado_en TIMESTAMPTZ NOT NULL DEFAULT now();
    UPDATE detalle_orden d SET creado_en = o.creado_en FROM orden o WHERE o.id_orden = d.id_orden;
    -- El UPDATE deja pendiente la revisión diferida del monto (trg_detalle_monto); se ejecuta ya, porque
    -- PostgreSQL no permite ALTER TABLE sobre una tabla con revisiones pendientes (07_seguridad/003_rls.sql).
    SET CONSTRAINTS ALL IMMEDIATE;
    SET CONSTRAINTS ALL DEFERRED;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                  WHERE table_schema = 'public' AND table_name = 'pago' AND column_name = 'creado_en') THEN
    ALTER TABLE pago ADD COLUMN creado_en TIMESTAMPTZ NOT NULL DEFAULT now();
    -- Rellenar una fecha no es un cambio del pago: se apaga el trigger de actualizado_en mientras tanto
    -- (DISABLE TRIGGER solo pide ser dueño de la tabla; no requiere superusuario, como en Neon).
    IF EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid = 'pago'::regclass AND tgname = 'trg_pago_upd') THEN
      ALTER TABLE pago DISABLE TRIGGER trg_pago_upd;
    END IF;
    UPDATE pago p SET creado_en = o.creado_en FROM orden o WHERE o.id_orden = p.id_orden;
    IF EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid = 'pago'::regclass AND tgname = 'trg_pago_upd') THEN
      ALTER TABLE pago ENABLE TRIGGER trg_pago_upd;
    END IF;
  END IF;
END $$;

-- Reglas de orden entre fechas para las columnas nuevas
DO $$
DECLARE
  r RECORD;
BEGIN
  FOR r IN
    SELECT v.tabla, v.nombre, v.regla
      FROM (VALUES
        ('rol',               'ck_rol_fechas',               'actualizado_en >= creado_en'),
        ('categoria',         'ck_categoria_fechas',         'actualizado_en >= creado_en'),
        ('horario_cafeteria', 'ck_horario_cafeteria_fechas', 'actualizado_en >= creado_en'),
        ('pago',              'ck_pago_creado',              'actualizado_en >= creado_en AND pagado_en >= creado_en')
      ) AS v(tabla, nombre, regla)
  LOOP
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conrelid = r.tabla::regclass AND conname = r.nombre) THEN
      EXECUTE format('ALTER TABLE %I ADD CONSTRAINT %I CHECK (%s)', r.tabla, r.nombre, r.regla);
    END IF;
  END LOOP;
END $$;
