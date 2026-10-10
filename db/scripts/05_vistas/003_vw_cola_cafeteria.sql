-- JY-Sprint2 09/10/2026: pantalla de pedidos de la cafetería (sin código de recogida). Lo usa sp_cola_cafeteria.
-- total sale de pago.monto: es el único total guardado (ver 03_cambios_sprint2/004_quitar_total_orden.sql).
DROP VIEW IF EXISTS vw_cola_cafeteria;
CREATE VIEW vw_cola_cafeteria AS
SELECT o.id_orden, o.id_cafeteria, o.estado, o.recogida_en, o.notas, u.nombre AS alumno,
       jsonb_agg(jsonb_build_object('producto', p.nombre, 'cantidad', d.cantidad)
                 ORDER BY p.nombre) AS productos,
       pg.monto AS total, pg.metodo, pg.estado AS estado_pago
  FROM orden o
  JOIN usuario u       ON u.id_usuario = o.id_usuario
  JOIN detalle_orden d ON d.id_orden = o.id_orden
  JOIN producto p      ON p.id_producto = d.id_producto
  JOIN pago pg         ON pg.id_orden = o.id_orden
 WHERE o.estado IN ('confirmada','en_preparacion','lista')
 GROUP BY o.id_orden, u.nombre, pg.monto, pg.metodo, pg.estado;
