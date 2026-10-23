-- JY-Sprint3 20/10/2026: TEC-08. Cafetería con sus métodos de pago en una fila, activas e inactivas.
-- La usan sp_listar_cafeterias y sp_obtener_cafeteria, así los dos devuelven exactamente la misma forma.
DROP VIEW IF EXISTS vw_cafeteria;
CREATE VIEW vw_cafeteria AS
SELECT c.id_cafeteria, c.nombre, c.ubicacion, c.es_del_campus, c.punto_entrega, c.activa,
       COALESCE(array_agg(m.metodo ORDER BY m.metodo) FILTER (WHERE m.metodo IS NOT NULL),
                '{}'::metodo_pago[]) AS metodos_pago,
       c.creado_en, c.actualizado_en
  FROM cafeteria c
  LEFT JOIN cafeteria_metodo_pago m ON m.id_cafeteria = c.id_cafeteria
 GROUP BY c.id_cafeteria;
