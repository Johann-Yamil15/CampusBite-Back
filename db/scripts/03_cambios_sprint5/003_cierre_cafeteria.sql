-- JJ-Sprint5 13/11/2026: "Cerrar por hoy". Una fila = la cafetería no abre ese día (fecha local del campus).
-- Los pedidos ya aceptados no se cancelan; solo se dejan de recibir nuevos.
CREATE TABLE IF NOT EXISTS cierre_cafeteria (
  id_cafeteria UUID        NOT NULL REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  fecha        DATE        NOT NULL,
  creado_por   UUID        NOT NULL REFERENCES usuario(id_usuario),
  creado_en    TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (id_cafeteria, fecha)
);
