-- JY-Sprint3 20/10/2026: TEC-08, hallazgo de las pruebas de estrés (db/tests/estres).
-- Para dar de baja una cafetería, sp_cambiar_activa_cafeteria cuenta sus órdenes en curso
-- (pendiente, confirmada, en_preparacion, lista). idx_orden_cola_cafeteria no incluía 'pendiente', así que
-- con 500 mil órdenes esa cuenta recorría la tabla completa (Seq Scan, ~25 ms; crece con cada orden).
-- El índice nuevo cubre los 4 estados. La cola de la cafetería (3 estados) lo sigue usando, porque sus estados
-- están dentro de los 4. Sigue siendo parcial: solo indexa las órdenes vivas, no el historial completo.
DO $$
BEGIN
  IF to_regclass('public.idx_orden_en_curso') IS NULL THEN
    CREATE INDEX idx_orden_en_curso ON orden (id_cafeteria, recogida_en)
     WHERE estado IN ('pendiente','confirmada','en_preparacion','lista');
  END IF;
END $$;

DROP INDEX IF EXISTS idx_orden_cola_cafeteria;
