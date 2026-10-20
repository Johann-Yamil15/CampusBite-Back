-- JY-Sprint2 09/10/2026: tipos ENUM. CREATE TYPE no tiene IF NOT EXISTS, por eso va en un bloque DO.
-- Para agregar un valor nuevo a un ENUM: ALTER TYPE ... ADD VALUE IF NOT EXISTS, en un script nuevo.
DO $$
BEGIN
  IF to_regtype('public.estado_orden') IS NULL THEN
    CREATE TYPE estado_orden AS ENUM
      ('pendiente','confirmada','en_preparacion','lista','entregada','cancelada');
  END IF;

  IF to_regtype('public.metodo_pago') IS NULL THEN
    CREATE TYPE metodo_pago AS ENUM ('tarjeta','transferencia','efectivo');
  END IF;

  IF to_regtype('public.estado_pago') IS NULL THEN
    CREATE TYPE estado_pago AS ENUM
      ('pendiente','pagado','rechazado','reembolsado','cancelado');
  END IF;
END $$;
