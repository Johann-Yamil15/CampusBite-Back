-- JY-Sprint2 09/10/2026: historial de órdenes del alumno. Lo usa sp_mis_ordenes, que siempre filtra por id_usuario.
-- total sale de pago.monto (un renglón por orden): no hay que sumar el detalle en cada consulta.
DROP VIEW IF EXISTS vw_mis_ordenes;
CREATE VIEW vw_mis_ordenes AS
SELECT o.id_orden, o.id_usuario, c.nombre AS cafeteria, c.punto_entrega, o.estado,
       o.recogida_en, o.codigo_recogida, pg.monto AS total, pg.metodo, pg.estado AS estado_pago, o.creado_en
  FROM orden o
  JOIN cafeteria c ON c.id_cafeteria = o.id_cafeteria
  JOIN pago pg     ON pg.id_orden = o.id_orden;
