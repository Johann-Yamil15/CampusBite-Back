# Pruebas de carga con k6

Scripts de [k6](https://k6.io) para el back de CampusBite. **Solo contra un ambiente de pruebas local o desechable**
(escriben en la base y generan mucho tráfico): nunca contra Azure ni Neon de producción.

| Script | Qué mide | Escribe en la BD |
|---|---|---|
| `humo.js` | 1 usuario recorre los endpoints principales; verifica que el ambiente está listo | No |
| `carga.js` | Carga normal: 80 % alumnos y 20 % encargados leyendo, con pausas realistas | No |
| `estres.js` | Sube la llegada de peticiones por escalones hasta el punto de quiebre | No |
| `picos.js` | Salto de 5 a 150 usuarios (la hora del almuerzo) y recuperación | No |
| `escritura.js` | Crear/cancelar pedidos y crear/borrar platillos en paralelo | **Sí** (limpia lo que crea) |
| `limite-auth.js` | Fuerza bruta al login: debe pasar de 401 a 429 | No |

## Preparar el ambiente

1. PostgreSQL desechable con el consolidado local (trae los datos de `90_datos_dev`, que usan los scripts):

   ```bash
   podman run -d --name cb-pg-carga -e POSTGRES_HOST_AUTH_METHOD=trust -p 55434:5432 postgres:16-alpine
   podman exec cb-pg-carga psql -U postgres -c "CREATE DATABASE campusbite"
   sed '/^[\\]/d' db/consolidado/versiones/campusbite_V1.0.0.012.sql | podman exec -i cb-pg-carga psql -U postgres -d campusbite -q -v ON_ERROR_STOP=1
   ```

2. Back local con una llave JWT **solo de pruebas** (la misma se le pasa a k6) y, para las pruebas de carga, sin el
   límite de intentos del login (que es para fuerza bruta, no para tráfico normal):

   ```bash
   export Jwt__Key="llave-solo-para-pruebas-locales-0123456789abcdef"
   ASPNETCORE_ENVIRONMENT=Development Database__Conexion=Local \
   ConnectionStrings__Local="Host=<ip>;Port=55434;Database=campusbite;Username=app_backend;Password=<clave de desarrollo, ver .env.example>" \
   ASPNETCORE_URLS=http://0.0.0.0:5167 dotnet run --project src/CampusBite.Api --no-launch-profile
   ```

   Para `limite-auth.js` NO pongas `RateLimit__AuthPermitLimit`: debe quedar el valor por defecto (20 por minuto).

## Ejecutar

Con k6 instalado (`winget install k6`):

```bash
k6 run -e JWT_KEY=$Jwt__Key tests/carga-k6/humo.js
k6 run -e JWT_KEY=$Jwt__Key tests/carga-k6/carga.js
k6 run -e JWT_KEY=$Jwt__Key -e VUS_ALUMNOS=80 -e VUS_ENCARGADOS=20 -e DURACION=5m tests/carga-k6/carga.js
```

O sin instalar nada, con el contenedor oficial (desde la raíz del repo; en Windows/Podman el back se alcanza con
`host.containers.internal`):

```bash
podman run --rm -i -v "$PWD/tests/carga-k6:/scripts" -w /scripts grafana/k6 run \
  -e BASE_URL=http://host.containers.internal:5167 -e JWT_KEY=$Jwt__Key humo.js
```

Cada script deja un resumen en `resultados/<nombre>.json` (ignorado por git).

## Cómo leer los resultados

- **Umbrales (thresholds):** lecturas con p95 < 500 ms y p99 < 1 s; escrituras con p95 < 1 s; menos de 1 % de errores.
  Si un umbral falla, k6 termina con código distinto de 0 (sirve para CI).
- **`estres.js`** no tiene un "debe pasar": el escalón donde se corta (p95 > 2 s o > 5 % de errores) es el límite del
  ambiente en el que lo corriste. Compáralo entre versiones, no con un número fijo.
- **`picos.js`:** además del pico, mira que la latencia de la última fase (recuperación) vuelva a ser la de la calma.
- Los números dependen de la máquina y de que la BD sea local: úsalos para comparar antes/después de un cambio, no como
  garantía de producción.
