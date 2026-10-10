-- JY-Sprint2 09/10/2026: orden.total era un dato derivado (la suma de detalle_orden.subtotal) y además
-- se repetía en pago.monto. Se queda un solo total guardado: pago.monto, que es lo que se cobra.
-- La regla pago.monto = suma del detalle la vigila 04_triggers/007_validar_monto_pago.sql.
-- Las vistas que leían orden.total se quitaron en 000_retirar_objetos_v1.sql y vuelven en 05_vistas.
ALTER TABLE orden DROP COLUMN IF EXISTS total;
