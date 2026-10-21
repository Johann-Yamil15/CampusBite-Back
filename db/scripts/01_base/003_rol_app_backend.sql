-- JY-Sprint2 09/10/2026: rol con el que se conecta la API. Nace sin LOGIN: la clave se asigna fuera del repo
-- (en local la pone 90_datos_dev/001_login_app_backend.sql).
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_backend') THEN
    CREATE ROLE app_backend NOLOGIN;
  END IF;
END $$;
