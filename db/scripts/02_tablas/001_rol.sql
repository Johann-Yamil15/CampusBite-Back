-- JY-Sprint2 09/10/2026: catálogo de roles (el id coincide con el enum RolUsuario del backend)
CREATE TABLE IF NOT EXISTS rol (
  id_rol  SMALLINT    PRIMARY KEY,
  nombre  VARCHAR(30) NOT NULL UNIQUE
);

INSERT INTO rol (id_rol, nombre) VALUES
  (1,'alumno'), (2,'admin_cafeteria'), (3,'admin_sistema')
ON CONFLICT (id_rol) DO NOTHING;
