-- =====================================================================
-- CampusBite · TEC-03 · Migración V003: vistas y seguridad
-- Vistas de consulta y permisos mínimos para el rol app_backend.
-- Origen: campusbite_TEC-02.sql (PostgreSQL 14+). Se aplica en una sola
-- transacción desde migrate.sh (psql -1 -v ON_ERROR_STOP=1).
-- =====================================================================

-- 5. Vistas de consulta --------------------------------------------------
CREATE VIEW vw_menu_cafeteria AS
SELECT c.id_cafeteria, c.nombre AS cafeteria, c.es_del_campus, c.punto_entrega,
       cat.nombre AS categoria, p.id_producto, p.nombre AS producto,
       p.descripcion, p.precio, p.imagen_url
  FROM producto p
  JOIN cafeteria c ON c.id_cafeteria = p.id_cafeteria AND c.activa
  LEFT JOIN categoria cat ON cat.id_categoria = p.id_categoria
 WHERE p.disponible;

CREATE VIEW vw_cola_cafeteria AS      -- pantalla de la cafetería (sin código de recogida)
SELECT o.id_orden, o.id_cafeteria, o.estado, o.hora_recogida, o.notas, u.nombre AS alumno,
       jsonb_agg(jsonb_build_object('producto', p.nombre, 'cantidad', d.cantidad)
                 ORDER BY p.nombre) AS productos,
       o.total, pg.metodo, pg.estado AS estado_pago
  FROM orden o
  JOIN usuario u        ON u.id_usuario = o.id_usuario
  JOIN detalle_orden d  ON d.id_orden = o.id_orden
  JOIN producto p       ON p.id_producto = d.id_producto
  JOIN pago pg          ON pg.id_orden = o.id_orden
 WHERE o.estado IN ('confirmada','en_preparacion','lista')
 GROUP BY o.id_orden, u.nombre, pg.metodo, pg.estado;

CREATE VIEW vw_mis_ordenes AS         -- filtrar siempre por id_usuario
SELECT o.id_orden, o.id_usuario, c.nombre AS cafeteria, c.punto_entrega, o.estado,
       o.hora_recogida, o.codigo_recogida, o.total, pg.metodo, pg.estado AS estado_pago, o.creada_en
  FROM orden o
  JOIN cafeteria c ON c.id_cafeteria = o.id_cafeteria
  JOIN pago pg     ON pg.id_orden = o.id_orden;

-- 6. Seguridad: el backend solo ejecuta procedimientos y lee vistas -----
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_backend') THEN
    CREATE ROLE app_backend NOLOGIN;   -- en producción: LOGIN PASSWORD '...'
  END IF;
END $$;

REVOKE ALL ON ALL TABLES    IN SCHEMA public FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO app_backend;
GRANT SELECT ON vw_menu_cafeteria, vw_cola_cafeteria, vw_mis_ordenes TO app_backend;
GRANT EXECUTE ON FUNCTION
  sp_registrar_usuario(VARCHAR,VARCHAR,VARCHAR,VARCHAR,VARCHAR),
  sp_obtener_credenciales(VARCHAR),
  sp_crear_orden(UUID,UUID,TIMESTAMPTZ,metodo_pago,JSONB,VARCHAR),
  sp_confirmar_pago(UUID,VARCHAR),
  sp_cambiar_estado_orden(UUID,estado_orden,UUID),
  sp_cancelar_orden(UUID,UUID),
  sp_entregar_orden(UUID,VARCHAR,UUID)
TO app_backend;


-- Mejora TEC-03: las funciones que se creen en migraciones futuras tampoco
-- serán ejecutables por PUBLIC (REVOKE ... ON ALL FUNCTIONS solo afecta a las
-- ya existentes). Cada migración nueva debe hacer su propio GRANT a app_backend.
ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
