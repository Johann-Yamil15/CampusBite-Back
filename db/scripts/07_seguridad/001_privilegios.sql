-- JY-Sprint2 09/10/2026: la API (app_backend) solo ejecuta los procedimientos de esta lista. No lee tablas
-- ni vistas. Se quita todo y se vuelve a dar, así el resultado es el mismo sin importar cuántas veces corra.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON ALL TABLES    IN SCHEMA public FROM PUBLIC, app_backend;   -- incluye vistas
REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM PUBLIC, app_backend;
REVOKE ALL ON ALL ROUTINES  IN SCHEMA public FROM PUBLIC, app_backend;   -- funciones y procedimientos

-- Corrige V003/V004: "ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ... FROM PUBLIC" no hace nada,
-- porque el permiso de PUBLIC viene del valor global y por esquema solo se pueden AGREGAR permisos.
-- Por eso sp_mis_ordenes y sp_cola_cafeteria quedaron ejecutables por cualquier rol. Esta es la forma
-- correcta: sin IN SCHEMA, para lo que cree en adelante el rol que ejecuta los scripts.
ALTER DEFAULT PRIVILEGES REVOKE EXECUTE ON ROUTINES FROM PUBLIC;

GRANT USAGE ON SCHEMA public TO app_backend;
GRANT EXECUTE ON PROCEDURE
  sp_registrar_usuario,
  sp_obtener_credenciales,
  sp_menu_cafeteria,
  sp_crear_orden,
  sp_confirmar_pago,
  sp_cambiar_estado_orden,
  sp_cancelar_orden,
  sp_entregar_orden,
  sp_mis_ordenes,
  sp_cola_cafeteria,
  sp_bitacora_registro,
  sp_listar_cafeterias,
  sp_obtener_cafeteria,
  sp_crear_cafeteria,
  sp_actualizar_cafeteria,
  sp_cambiar_activa_cafeteria,
  sp_restablecer_password,
  sp_autorregistrar_cafeteria,
  sp_actualizar_imagen_producto,
  sp_crear_producto,
  sp_listar_productos_cafeteria,
  sp_listar_categorias,
  sp_actualizar_producto,
  sp_toggle_disponible_producto,
  sp_eliminar_producto,
  sp_crear_categoria,
  sp_eliminar_categoria
TO app_backend;
-- Internos (no se dan a app_backend): sp_exigir_gestor_cafeteria, sp_exigir_admin_sistema, sp_validar_datos_cafeteria,
-- sp_validar_datos_producto.

-- Nadie entra a la base sin permiso explícito. Si quien ejecuta no es dueño de la base, solo se avisa.
DO $$
BEGIN
  EXECUTE format('REVOKE ALL ON DATABASE %I FROM PUBLIC', current_database());
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO app_backend', current_database());
EXCEPTION WHEN insufficient_privilege THEN
  RAISE WARNING 'No se pudieron ajustar los permisos de la base (no eres su dueño); hazlo a mano.';
END $$;
