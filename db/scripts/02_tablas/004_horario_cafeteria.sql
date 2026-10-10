-- JY-Sprint2 09/10/2026: horario semanal. Las horas son hora local del campus (America/Mexico_City).
CREATE TABLE IF NOT EXISTS horario_cafeteria (
  id_horario    UUID     PRIMARY KEY DEFAULT gen_random_uuid(),
  id_cafeteria  UUID     NOT NULL REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  dia_semana    SMALLINT NOT NULL CHECK (dia_semana BETWEEN 1 AND 7),  -- ISO: 1 = lunes
  hora_apertura TIME     NOT NULL,
  hora_cierre   TIME     NOT NULL,
  CHECK (hora_cierre > hora_apertura),
  UNIQUE (id_cafeteria, dia_semana, hora_apertura)
);
