-- JY-Sprint2 09/10/2026: bitácora automática de estados. Quién hizo el cambio llega en app.id_actor,
-- que fijan los procedimientos con set_config(..., true) (vale solo dentro de la transacción).
CREATE OR REPLACE FUNCTION fn_historial_orden() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  INSERT INTO historial_estado_orden (id_orden, estado, cambiado_por)
  VALUES (NEW.id_orden, NEW.estado,
          NULLIF(current_setting('app.id_actor', true), '')::uuid);
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_orden_historial_ins AFTER INSERT ON orden
  FOR EACH ROW EXECUTE FUNCTION fn_historial_orden();
CREATE OR REPLACE TRIGGER trg_orden_historial_upd AFTER UPDATE OF estado ON orden
  FOR EACH ROW WHEN (OLD.estado IS DISTINCT FROM NEW.estado)
  EXECUTE FUNCTION fn_historial_orden();
