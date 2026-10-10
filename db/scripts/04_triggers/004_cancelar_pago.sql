-- JY-Sprint2 09/10/2026: al cancelar una orden se cierra su pago (reembolso si ya estaba pagado)
CREATE OR REPLACE FUNCTION fn_cancelar_pago() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  UPDATE pago
     SET estado    = CASE WHEN estado = 'pagado' THEN 'reembolsado'::estado_pago
                          ELSE 'cancelado'::estado_pago END,
         pagado_en = CASE WHEN estado = 'pagado' THEN pagado_en ELSE NULL END
   WHERE id_orden = NEW.id_orden AND estado IN ('pendiente','pagado');
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_orden_cancelada AFTER UPDATE OF estado ON orden
  FOR EACH ROW WHEN (NEW.estado = 'cancelada' AND OLD.estado <> 'cancelada')
  EXECUTE FUNCTION fn_cancelar_pago();
