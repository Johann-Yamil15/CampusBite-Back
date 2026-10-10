-- JY-Sprint2 09/10/2026: mantiene actualizado_en. orden ya no lo tiene (ver 03_cambios_sprint2/005_fechas.sql).
CREATE OR REPLACE FUNCTION fn_set_actualizado_en() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  NEW.actualizado_en := now();
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_usuario_upd   BEFORE UPDATE ON usuario   FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE OR REPLACE TRIGGER trg_cafeteria_upd BEFORE UPDATE ON cafeteria FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE OR REPLACE TRIGGER trg_producto_upd  BEFORE UPDATE ON producto  FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE OR REPLACE TRIGGER trg_pago_upd      BEFORE UPDATE ON pago      FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
-- JY-Sprint2 10/10/2026: tablas que recibieron actualizado_en en 03_cambios_sprint2/006_fechas_en_todas_las_tablas.sql
CREATE OR REPLACE TRIGGER trg_rol_upd       BEFORE UPDATE ON rol               FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE OR REPLACE TRIGGER trg_categoria_upd BEFORE UPDATE ON categoria         FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE OR REPLACE TRIGGER trg_horario_upd   BEFORE UPDATE ON horario_cafeteria FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
