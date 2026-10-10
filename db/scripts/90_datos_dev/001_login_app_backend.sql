-- JJ-Sprint2 09/10/2026: login de la API en desarrollo local (mismo que el changeSet "dev-login-app-backend" de Liquibase).
-- La API se conecta con: Username=app_backend;Password=app_backend_dev. NUNCA usar esta clave fuera de local.
ALTER ROLE app_backend WITH LOGIN PASSWORD 'app_backend_dev';
