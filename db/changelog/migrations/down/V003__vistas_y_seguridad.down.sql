-- Rollback de V003
DROP VIEW IF EXISTS vw_mis_ordenes, vw_cola_cafeteria, vw_menu_cafeteria;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT EXECUTE ON FUNCTIONS TO PUBLIC;
-- El rol app_backend NO se elimina aquí: pertenece al servidor, no al esquema.
