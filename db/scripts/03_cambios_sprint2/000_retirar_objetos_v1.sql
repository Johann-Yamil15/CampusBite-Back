-- JY-Sprint2 09/10/2026: retira lo que V1.0.0.001 creó y Sprint 2 rehace de otra forma.
--  * Los sp_* eran FUNCTION; ahora son PROCEDURE (06_procedimientos). Una función y un procedimiento
--    no pueden convivir con el mismo nombre y argumentos, así que se borran las funciones sp_* que queden.
--    No se usa DROP FUNCTION IF EXISTS: falla con "is not a function" si ya existe el procedimiento.
--  * Las vistas se recrean en 05_vistas. Se quitan aquí porque leen columnas que 03_cambios_sprint2
--    elimina o renombra (orden.total, hora_recogida, creada_en) y PostgreSQL no lo permitiría.
DO $$
DECLARE
  v_funcion REGPROCEDURE;
BEGIN
  FOR v_funcion IN
    SELECT p.oid::regprocedure
      FROM pg_proc p
     WHERE p.pronamespace = 'public'::regnamespace
       AND p.prokind = 'f'
       AND p.proname LIKE 'sp\_%'
  LOOP
    EXECUTE format('DROP FUNCTION %s', v_funcion);
  END LOOP;
END $$;

DROP VIEW IF EXISTS vw_mis_ordenes, vw_cola_cafeteria, vw_menu_cafeteria;
