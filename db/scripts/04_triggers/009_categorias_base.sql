-- JJ-Sprint5 15/11/2026: toda cafetería nace con 5 categorías base (Desayunos, Comidas, Bebidas, Snacks, Postres).
-- Antes un negocio recién registrado no tenía ninguna y el formulario de producto solo ofrecía "Sin categoría".
-- Va en un trigger para cubrir a la vez sp_crear_cafeteria (admin) y sp_autorregistrar_cafeteria (registro).
-- ON CONFLICT sin destino: también respeta ux_categoria_nombre (sin distinguir mayúsculas).
CREATE OR REPLACE FUNCTION fn_categorias_base() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  INSERT INTO categoria (id_cafeteria, nombre)
  SELECT NEW.id_cafeteria, b.nombre
    FROM (VALUES ('Desayunos'), ('Comidas'), ('Bebidas'), ('Snacks'), ('Postres')) AS b(nombre)
  ON CONFLICT DO NOTHING;
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_cafeteria_categorias_base AFTER INSERT ON cafeteria
  FOR EACH ROW EXECUTE FUNCTION fn_categorias_base();

-- Relleno: las cafeterías que ya existen y no tienen NINGUNA categoría reciben las 5 base.
-- Las que ya tienen alguna no se tocan (su encargado ya las armó a su gusto).
INSERT INTO categoria (id_cafeteria, nombre)
SELECT c.id_cafeteria, b.nombre
  FROM cafeteria c
 CROSS JOIN (VALUES ('Desayunos'), ('Comidas'), ('Bebidas'), ('Snacks'), ('Postres')) AS b(nombre)
 WHERE NOT EXISTS (SELECT 1 FROM categoria k WHERE k.id_cafeteria = c.id_cafeteria)
ON CONFLICT DO NOTHING;
