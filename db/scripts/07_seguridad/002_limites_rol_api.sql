-- JY-Sprint2 09/10/2026: una consulta o transacción colgada de la API no tumba el servicio.
-- En servicios administrados (Neon, Azure) quien migra puede no tener permiso sobre el rol: solo se avisa.
DO $$
BEGIN
  ALTER ROLE app_backend CONNECTION LIMIT 50;
  ALTER ROLE app_backend SET statement_timeout = '10s';
  ALTER ROLE app_backend SET lock_timeout = '3s';
  ALTER ROLE app_backend SET idle_in_transaction_session_timeout = '15s';
EXCEPTION WHEN insufficient_privilege THEN
  RAISE WARNING 'No se pudieron fijar los límites de app_backend (falta permiso sobre el rol); hazlo a mano.';
END $$;
