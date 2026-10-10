-- JY-Sprint2 09/10/2026: acepta_efectivo, acepta_tarjeta y acepta_transferencia eran un grupo repetido
-- (una columna por cada valor de metodo_pago). Agregar un método obligaba a agregar otra columna.
-- Ahora cada método aceptado es una fila. La validación del método vive en sp_crear_orden.
CREATE TABLE IF NOT EXISTS cafeteria_metodo_pago (
  id_cafeteria UUID        NOT NULL REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  metodo       metodo_pago NOT NULL,
  PRIMARY KEY (id_cafeteria, metodo)
);

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'cafeteria' AND column_name = 'acepta_efectivo') THEN
    INSERT INTO cafeteria_metodo_pago (id_cafeteria, metodo)
    SELECT c.id_cafeteria, m.metodo
      FROM cafeteria c
     CROSS JOIN LATERAL (VALUES ('efectivo'::metodo_pago, c.acepta_efectivo),
                                ('tarjeta'::metodo_pago, c.acepta_tarjeta),
                                ('transferencia'::metodo_pago, c.acepta_transferencia)) AS m(metodo, acepta)
     WHERE m.acepta
    ON CONFLICT (id_cafeteria, metodo) DO NOTHING;

    -- El CHECK (acepta_efectivo OR ...) se borra junto con sus columnas.
    ALTER TABLE cafeteria
      DROP COLUMN acepta_efectivo,
      DROP COLUMN acepta_tarjeta,
      DROP COLUMN acepta_transferencia;
  END IF;
END $$;
