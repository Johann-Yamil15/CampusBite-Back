-- JY-Sprint2 09/10/2026: orden.id_cafeteria se podría deducir de sus productos, pero se guarda porque la
-- orden necesita su cafetería antes de tener renglones (horario, método de pago) y porque la cola de la
-- cafetería se busca por ella (idx_orden_cola_cafeteria). Para que esa copia no se desincronice:
-- fn_validar_detalle revisa cada renglón al insertarlo, y este trigger impide cambiar después la
-- cafetería de una orden o de un producto (eso rompería los renglones ya validados).
CREATE OR REPLACE FUNCTION fn_bloquear_cambio_cafeteria() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  IF NEW.id_cafeteria IS DISTINCT FROM OLD.id_cafeteria THEN
    RAISE EXCEPTION 'No se puede cambiar la cafetería de % (crea uno nuevo)', TG_TABLE_NAME
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_orden_cafeteria_fija BEFORE UPDATE OF id_cafeteria ON orden
  FOR EACH ROW EXECUTE FUNCTION fn_bloquear_cambio_cafeteria();
CREATE OR REPLACE TRIGGER trg_producto_cafeteria_fija BEFORE UPDATE OF id_cafeteria ON producto
  FOR EACH ROW EXECUTE FUNCTION fn_bloquear_cambio_cafeteria();
