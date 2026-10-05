-- =====================================================================
-- CampusBite · V005 · Depuración de índices (basada en mediciones)
-- Criterio: se queda un índice solo si lo usa una consulta real del sistema o
-- respalda una restricción. Medido con 300 mil órdenes y pg_stat_user_indexes.
--
--  idx_horario_cafeteria_dia  redundante: el UNIQUE (id_cafeteria, dia_semana, hora_apertura)
--                             ya sirve las mismas búsquedas por prefijo.
--  idx_usuario_rol            la columna tiene 3 valores (rol): cero escaneos, nunca selectivo.
--  idx_cafeteria_activa       tabla de decenas de filas; el barrido secuencial es más rápido.
--  idx_cafeteria_admin        igual; las validaciones de permiso entran por la PK de cafeteria.
--  idx_producto_categoria     tabla pequeña y categorías casi nunca se borran (FK SET NULL).
--
-- Se conservan, por uso comprobado: idx_orden_usuario_fecha (mis órdenes),
-- idx_orden_cola_cafeteria (parcial, cola de la cafetería), idx_producto_cafeteria
-- (parcial, menú), idx_historial_orden_fecha (línea de tiempo), idx_detalle_producto
-- (FK de la tabla más grande), idx_push_usuario (envío de notificaciones).
-- =====================================================================
DROP INDEX IF EXISTS idx_horario_cafeteria_dia;
DROP INDEX IF EXISTS idx_usuario_rol;
DROP INDEX IF EXISTS idx_cafeteria_activa;
DROP INDEX IF EXISTS idx_cafeteria_admin;
DROP INDEX IF EXISTS idx_producto_categoria;
