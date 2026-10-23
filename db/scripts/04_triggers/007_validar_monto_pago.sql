-- JY-Sprint2 09/10/2026: pago.monto es el único total guardado (orden.total se quitó). Esta regla garantiza
-- que siempre sea igual a la suma de detalle_orden.subtotal.
-- Es un CONSTRAINT TRIGGER diferido: se revisa al hacer COMMIT, cuando la orden ya tiene detalle y pago.
-- CREATE OR REPLACE no existe para constraint triggers, por eso se borran y se crean.
-- SECURITY DEFINER (JY-Sprint2 10/10/2026): al ser diferido, corre en el COMMIT, ya fuera del procedimiento,
-- con los permisos de quien llamó (app_backend, que no puede leer pago). Sin esto, toda orden creada desde
-- la API fallaba al confirmar la transacción. Solo lee pago y detalle_orden.
CREATE OR REPLACE FUNCTION fn_validar_monto_pago() RETURNS trigger
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_orden UUID := CASE WHEN TG_OP = 'DELETE' THEN OLD.id_orden ELSE NEW.id_orden END;
  v_monto    NUMERIC(10,2);
  v_suma     NUMERIC(10,2);
BEGIN
  SELECT p.monto INTO v_monto FROM pago p WHERE p.id_orden = v_id_orden;
  IF NOT FOUND THEN RETURN NULL; END IF;     -- la orden se borró (cascada) o no tiene pago

  SELECT COALESCE(sum(d.subtotal), 0) INTO v_suma FROM detalle_orden d WHERE d.id_orden = v_id_orden;
  IF v_monto <> v_suma THEN
    RAISE EXCEPTION 'El monto del pago (%) no coincide con el detalle de la orden (%)', v_monto, v_suma
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NULL;
END $$;

DROP TRIGGER IF EXISTS trg_pago_monto ON pago;
CREATE CONSTRAINT TRIGGER trg_pago_monto
  AFTER INSERT OR UPDATE OF monto ON pago
  DEFERRABLE INITIALLY DEFERRED
  FOR EACH ROW EXECUTE FUNCTION fn_validar_monto_pago();

DROP TRIGGER IF EXISTS trg_detalle_monto ON detalle_orden;
CREATE CONSTRAINT TRIGGER trg_detalle_monto
  AFTER INSERT OR UPDATE OF id_orden, cantidad, precio_unitario OR DELETE ON detalle_orden   -- lo que cambia el total
  DEFERRABLE INITIALLY DEFERRED
  FOR EACH ROW EXECUTE FUNCTION fn_validar_monto_pago();
