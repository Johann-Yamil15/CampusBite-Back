-- JY-Sprint2 09/10/2026: citext guarda el correo sin distinguir mayúsculas.
-- En Azure Database for PostgreSQL hay que permitirla antes en el parámetro azure.extensions.
CREATE EXTENSION IF NOT EXISTS citext;
