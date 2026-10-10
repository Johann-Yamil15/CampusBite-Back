-- JY-Sprint2 09/10/2026: índices con uso comprobado (medidos con 300 mil órdenes, ver db/README.md).
-- PostgreSQL no indexa las FK por su cuenta. Los 5 índices que V005 quitó ya no se crean.
-- No se usa CREATE INDEX IF NOT EXISTS: PostgreSQL valida las columnas aunque el índice ya exista, y
-- 03_cambios_sprint2/005_fechas.sql renombra creada_en y hora_recogida (el índice las sigue solo).
DO $$
DECLARE
  r RECORD;
BEGIN
  FOR r IN
    SELECT v.nombre, v.definicion
      FROM (VALUES
        ('idx_producto_cafeteria',    'ON producto(id_cafeteria, id_categoria) WHERE disponible'),
        ('idx_orden_usuario_fecha',   'ON orden(id_usuario, creada_en DESC)'),
        ('idx_orden_cola_cafeteria',  'ON orden(id_cafeteria, hora_recogida) WHERE estado IN (''confirmada'',''en_preparacion'',''lista'')'),
        ('idx_detalle_producto',      'ON detalle_orden(id_producto)'),        -- (id_orden) lo cubre el UNIQUE
        ('idx_historial_orden_fecha', 'ON historial_estado_orden(id_orden, cambiado_en)'),
        ('idx_push_usuario',          'ON suscripcion_push(id_usuario)')
      ) AS v(nombre, definicion)
  LOOP
    IF to_regclass('public.' || r.nombre) IS NULL THEN
      EXECUTE format('CREATE INDEX %I %s', r.nombre, r.definicion);
    END IF;
  END LOOP;
END $$;
