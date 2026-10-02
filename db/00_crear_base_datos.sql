-- =====================================================================
-- CampusBite · TEC-03 · Creación de la base de datos y del rol de la app
-- Ejecutar UNA vez como superusuario (o rol con CREATEDB/CREATEROLE):
--   psql -U postgres -f 00_crear_base_datos.sql
-- En Supabase/Neon la base ya existe: omitir este archivo y usar solo
-- migrate.sh.
-- =====================================================================

-- Rol con el que se conectará el backend (mínimo privilegio).
-- Cambiar la contraseña y activar LOGIN fuera de desarrollo.
SELECT 'CREATE ROLE app_backend NOLOGIN'
 WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_backend')
\gexec

-- Base de datos (CREATE DATABASE no puede ir dentro de DO/transacción).
SELECT 'CREATE DATABASE campusbite ENCODING ''UTF8'' TEMPLATE template0'
 WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'campusbite')
\gexec

ALTER DATABASE campusbite SET timezone TO 'UTC';
GRANT CONNECT ON DATABASE campusbite TO app_backend;
