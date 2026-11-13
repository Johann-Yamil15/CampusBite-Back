-- JY-Sprint4 31/10/2026: verificación de correo al registrarse (alumno y negocio).
-- correo_verificado: hasta que sea true el usuario no puede iniciar sesión.
-- verificacion_hash: SHA-256 (hex) del token que va en el enlace del correo; el token en claro nunca se guarda.
-- verificacion_expira: después de esta fecha el enlace ya no sirve y hay que pedir otro.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                  WHERE table_schema = 'public' AND table_name = 'usuario' AND column_name = 'correo_verificado') THEN
    -- Las cuentas que ya existían se dan por verificadas (se crearon antes de que existiera la verificación);
    -- desde ahora las nuevas nacen sin verificar
    ALTER TABLE usuario ADD COLUMN correo_verificado BOOLEAN NOT NULL DEFAULT true;
    ALTER TABLE usuario ALTER COLUMN correo_verificado SET DEFAULT false;
  END IF;
END $$;

ALTER TABLE usuario ADD COLUMN IF NOT EXISTS verificacion_hash   CHAR(64);
ALTER TABLE usuario ADD COLUMN IF NOT EXISTS verificacion_expira TIMESTAMPTZ;

-- Para encontrar al usuario por el token sin recorrer la tabla; parcial porque casi todos lo tienen en NULL
CREATE UNIQUE INDEX IF NOT EXISTS ux_usuario_verificacion_hash ON usuario (verificacion_hash)
  WHERE verificacion_hash IS NOT NULL;
