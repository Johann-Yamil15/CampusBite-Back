-- Rollback de V005
CREATE INDEX idx_horario_cafeteria_dia ON horario_cafeteria(id_cafeteria, dia_semana);
CREATE INDEX idx_usuario_rol           ON usuario(id_rol);
CREATE INDEX idx_cafeteria_activa      ON cafeteria(es_del_campus) WHERE activa;
CREATE INDEX idx_cafeteria_admin       ON cafeteria(id_admin);
CREATE INDEX idx_producto_categoria    ON producto(id_categoria);
