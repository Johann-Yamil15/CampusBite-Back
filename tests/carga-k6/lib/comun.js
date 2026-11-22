// Utilidades compartidas de las pruebas de carga (k6).
import crypto from 'k6/crypto';
import encoding from 'k6/encoding';

// ---- Configuración por variables de entorno (-e NOMBRE=valor) --------------------------------------------------------
export const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5167').replace(/\/$/, '');
// La MISMA llave con la que arrancó el back (Jwt__Key). Nunca la de producción: estas pruebas son solo para un ambiente
// de pruebas local o desechable.
const JWT_KEY = __ENV.JWT_KEY;
const ISSUER = __ENV.JWT_ISSUER || 'CampusBite.API';
const AUDIENCE = __ENV.JWT_AUDIENCE || 'CampusBite.PWA';

// Datos de db/scripts/90_datos_dev/002_datos_prueba.sql
export const CAFETERIA_ID = __ENV.CAFETERIA_ID || '00000000-0000-0000-0000-0000000000c1';
export const USUARIOS = {
  alumno: { id: '00000000-0000-0000-0000-0000000000a1', correo: 'alumno@ejemplo.test', rol: 'Alumno', nombre: 'Alumno de Prueba', idCafeteria: null },
  encargado: { id: '00000000-0000-0000-0000-0000000000a2', correo: 'cafeteria@ejemplo.test', rol: 'AdminCafeteria', nombre: 'Encargado Cafetería', idCafeteria: CAFETERIA_ID },
};

// ---- JWT HS256 (igual que el back) ----------------------------------------------------------------------------------
const b64 = (texto) => encoding.b64encode(texto, 'rawurl');

export function firmarJwt(usuario, segundosDeVida = 3600) {
  if (!JWT_KEY) {
    throw new Error('Define JWT_KEY con la misma llave Jwt__Key del back local (k6 run -e JWT_KEY=...)');
  }
  const ahora = Math.floor(Date.now() / 1000);
  const payload = {
    sub: usuario.id, email: usuario.correo, jti: `${usuario.id}-${ahora}`, role: usuario.rol, name: usuario.nombre,
    iss: ISSUER, aud: AUDIENCE, iat: ahora, nbf: ahora, exp: ahora + segundosDeVida,
  };
  if (usuario.idCafeteria) payload.id_cafeteria = usuario.idCafeteria;
  const cuerpo = `${b64(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))}.${b64(JSON.stringify(payload))}`;
  return `${cuerpo}.${crypto.hmac('sha256', JWT_KEY, cuerpo, 'base64rawurl')}`;
}

// Para que el resumen incluya p(99)
export const TENDENCIA = ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'];

export const cabeceras = (token, extra = {}) => ({ headers: { Authorization: `Bearer ${token}`, ...extra } });

// ---- Umbrales (SLO) por tipo de operación --------------------------------------------------------------------------
// Lecturas: p95 < 500 ms. Escrituras: p95 < 1 s. Errores: menos de 1 %.
export const UMBRALES_LECTURA = {
  http_req_failed: ['rate<0.01'],
  'http_req_duration{tipo:lectura}': ['p(95)<500', 'p(99)<1000'],
  checks: ['rate>0.99'],
};
export const UMBRALES_ESCRITURA = {
  'http_req_duration{tipo:escritura}': ['p(95)<1000', 'p(99)<2000'],
};

export function resumen(titulo) {
  return (datos) => {
    const m = datos.metrics;
    const v = (nombre, campo) => (m[nombre] && m[nombre].values[campo] !== undefined ? m[nombre].values[campo] : NaN);
    const linea = (e, x) => `${e.padEnd(26)} ${x}`;
    const texto = [
      `\n=== ${titulo} ===`,
      linea('peticiones', `${v('http_reqs', 'count')} (${v('http_reqs', 'rate').toFixed(1)}/s)`),
      linea('errores HTTP', `${(v('http_req_failed', 'rate') * 100).toFixed(2)} %`),
      linea('duración p50 / p95 / p99', `${v('http_req_duration', 'med').toFixed(0)} / ${v('http_req_duration', 'p(95)').toFixed(0)} / ${v('http_req_duration', 'p(99)').toFixed(0)} ms`),
      linea('duración máx.', `${v('http_req_duration', 'max').toFixed(0)} ms`),
      linea('checks', `${(v('checks', 'rate') * 100).toFixed(2)} %`),
      '',
    ].join('\n');
    return { stdout: texto, [`resultados/${titulo.replace(/\W+/g, '-')}.json`]: JSON.stringify(datos, null, 2) };
  };
}
