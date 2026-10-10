-- JY-Sprint2 09/10/2026: productos disponibles de las cafeterías activas (lo usa sp_menu_cafeteria)
DROP VIEW IF EXISTS vw_menu_cafeteria;
CREATE VIEW vw_menu_cafeteria AS
SELECT c.id_cafeteria, c.nombre AS cafeteria, c.es_del_campus, c.punto_entrega,
       cat.nombre AS categoria, p.id_producto, p.nombre AS producto,
       p.descripcion, p.precio, p.imagen_url
  FROM producto p
  JOIN cafeteria c        ON c.id_cafeteria = p.id_cafeteria AND c.activa
  LEFT JOIN categoria cat ON cat.id_categoria = p.id_categoria
 WHERE p.disponible;
