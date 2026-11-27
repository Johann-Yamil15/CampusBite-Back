-- JY-Sprint5 27/11/2026: la API puede ejecutar sp_destinatario_orden (avisos por correo de los pedidos).
-- Va aparte de 001_privilegios.sql, que quita y vuelve a dar todo: este corre después y solo agrega el permiso.
GRANT EXECUTE ON PROCEDURE sp_destinatario_orden TO app_backend;
