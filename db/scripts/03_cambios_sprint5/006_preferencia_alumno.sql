-- JJ-Sprint5 13/11/2026: preferencias del alumno. No se guardan datos de tarjetas, solo el método preferido.
CREATE TABLE IF NOT EXISTS preferencia_alumno (
  id_usuario             UUID        PRIMARY KEY REFERENCES usuario(id_usuario) ON DELETE CASCADE,
  id_cafeteria_frecuente UUID        REFERENCES cafeteria(id_cafeteria) ON DELETE SET NULL,
  metodo_pago            metodo_pago,
  aviso_pedido_listo     BOOLEAN     NOT NULL DEFAULT true,
  aviso_promociones      BOOLEAN     NOT NULL DEFAULT false,
  creado_en              TIMESTAMPTZ NOT NULL DEFAULT now(),
  actualizado_en         TIMESTAMPTZ NOT NULL DEFAULT now()
);
-- Convención: toda tabla tiene creado_en (por si la tabla ya existía sin ella)
ALTER TABLE preferencia_alumno ADD COLUMN IF NOT EXISTS creado_en TIMESTAMPTZ NOT NULL DEFAULT now();
