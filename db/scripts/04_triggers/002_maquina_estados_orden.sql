-- JY-Sprint2 09/10/2026: máquina de estados de la orden
CREATE OR REPLACE FUNCTION fn_validar_transicion_orden() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  IF NEW.estado = OLD.estado THEN RETURN NEW; END IF;
  IF NOT (
       (OLD.estado = 'pendiente'      AND NEW.estado IN ('confirmada','cancelada'))
    OR (OLD.estado = 'confirmada'     AND NEW.estado IN ('en_preparacion','cancelada'))
    OR (OLD.estado = 'en_preparacion' AND NEW.estado IN ('lista','cancelada'))
    OR (OLD.estado = 'lista'          AND NEW.estado IN ('entregada','cancelada'))
  ) THEN
    RAISE EXCEPTION 'Transición de estado no permitida: % -> %', OLD.estado, NEW.estado
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_orden_transicion BEFORE UPDATE OF estado ON orden
  FOR EACH ROW EXECUTE FUNCTION fn_validar_transicion_orden();
