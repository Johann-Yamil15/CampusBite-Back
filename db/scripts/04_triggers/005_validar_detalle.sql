-- JY-Sprint2 09/10/2026: un detalle solo puede llevar productos de la cafetería de su orden
-- (orden.id_cafeteria = producto.id_cafeteria). Junto con 006_cafeteria_inmutable.sql cierra el otro
-- círculo del modelo: orden -> cafeteria <- producto <- detalle_orden -> orden.
CREATE OR REPLACE FUNCTION fn_validar_detalle() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  IF (SELECT p.id_cafeteria FROM producto p WHERE p.id_producto = NEW.id_producto)
     IS DISTINCT FROM
     (SELECT o.id_cafeteria FROM orden o WHERE o.id_orden = NEW.id_orden) THEN
    RAISE EXCEPTION 'El producto no pertenece a la cafetería de la orden'
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_detalle_valida BEFORE INSERT OR UPDATE ON detalle_orden
  FOR EACH ROW EXECUTE FUNCTION fn_validar_detalle();
