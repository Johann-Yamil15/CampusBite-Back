-- JY-Sprint2 09/10/2026: pago de la orden (uno por orden). monto es el total cobrado.
CREATE TABLE IF NOT EXISTS pago (
  id_pago        UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  id_orden       UUID          NOT NULL UNIQUE REFERENCES orden(id_orden) ON DELETE CASCADE,
  metodo         metodo_pago   NOT NULL,
  monto          NUMERIC(10,2) NOT NULL CHECK (monto > 0),
  estado         estado_pago   NOT NULL DEFAULT 'pendiente',
  referencia     VARCHAR(100),
  pagado_en      TIMESTAMPTZ,
  actualizado_en TIMESTAMPTZ   NOT NULL DEFAULT now(),
  CHECK ((estado IN ('pagado','reembolsado')) = (pagado_en IS NOT NULL))
);
