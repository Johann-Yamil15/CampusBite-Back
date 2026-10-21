-- JY-Sprint2 09/10/2026: índices con uso comprobado (medidos con 300 mil órdenes, ver db/README.md).
-- PostgreSQL no indexa las FK por su cuenta. Los 5 índices que V005 quitó ya no se crean.
-- No se usa CREATE INDEX IF NOT EXISTS: PostgreSQL valida las columnas aunque el índice ya exista, y
-- 03_cambios_sprint2/005_fechas.sql renombra creada_en y hora_recogida (el índice las sigue solo).
-- JY-Sprint3 20/10/2026: "reemplazado_por": si un script posterior sustituyó el índice, no se vuelve a crear
-- (idx_orden_cola_cafeteria -> idx_orden_en_curso en 03_cambios_sprint3). Sin esto, la segunda ejecución
-- lo recreaba con la columna vieja hora_recogida y fallaba.
DO $$
DECLARE
  r RECORD;
BEGIN
  FOR r IN
    SELECT v.nombre, v.definicion, v.reemplazado_por
      FROM (VALUES
        ('idx_producto_cafeteria',    'ON producto(id_cafeteria, id_categoria) WHERE disponible', NULL),
        ('idx_orden_usuario_fecha',   'ON orden(id_usuario, creada_en DESC)', NULL),
        ('idx_orden_cola_cafeteria',  'ON orden(id_cafeteria, hora_recogida) WHERE estado IN (''confirmada'',''en_preparacion'',''lista'')', 'idx_orden_en_curso'),
        ('idx_detalle_producto',      'ON detalle_orden(id_producto)', NULL),        -- (id_orden) lo cubre el UNIQUE
        ('idx_historial_orden_fecha', 'ON historial_estado_orden(id_orden, cambiado_en)', NULL),
        ('idx_push_usuario',          'ON suscripcion_push(id_usuario)', NULL)
      ) AS v(nombre, definicion, reemplazado_por)
  LOOP
    IF to_regclass('public.' || r.nombre) IS NULL
       AND (r.reemplazado_por IS NULL OR to_regclass('public.' || r.reemplazado_por) IS NULL) THEN
      EXECUTE format('CREATE INDEX %I %s', r.nombre, r.definicion);
    END IF;
  END LOOP;
END $$;
