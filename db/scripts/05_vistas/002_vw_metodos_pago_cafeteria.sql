-- JY-Sprint2 09/10/2026: métodos de pago de cada cafetería activa, en una fila (reemplaza a acepta_*)
DROP VIEW IF EXISTS vw_metodos_pago_cafeteria;
CREATE VIEW vw_metodos_pago_cafeteria AS
SELECT c.id_cafeteria, array_agg(m.metodo ORDER BY m.metodo) AS metodos
  FROM cafeteria c
  JOIN cafeteria_metodo_pago m ON m.id_cafeteria = c.id_cafeteria
 WHERE c.activa
 GROUP BY c.id_cafeteria;
