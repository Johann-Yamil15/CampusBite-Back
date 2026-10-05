-- Rollback de V001 (destruye TODOS los datos)
DROP TABLE IF EXISTS suscripcion_push, historial_estado_orden, pago, detalle_orden,
                     orden, producto, categoria, horario_cafeteria, cafeteria,
                     usuario, rol CASCADE;
DROP TYPE IF EXISTS estado_pago, metodo_pago, estado_orden;
-- La extensión citext se conserva: puede ser compartida con otras bases/esquemas.
