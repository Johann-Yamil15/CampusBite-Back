// Fuerza bruta contra el login: muchos intentos fallidos desde una misma IP. El back debe responder 401 al principio y
// pasar a 429 (con Retry-After) al llegar al límite; nunca 5xx y nunca 200.
// IMPORTANTE: arranca el back con el límite por defecto (RateLimit__AuthPermitLimit=20 por 60 s). Si lo subiste para otras
// pruebas, esta no verá ningún 429.
//   k6 run tests/carga-k6/limite-auth.js
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import { BASE_URL, resumen, TENDENCIA } from './lib/comun.js';

const respuestas401 = new Counter('login_401');
const respuestas429 = new Counter('login_429');

export const options = {
  summaryTrendStats: TENDENCIA,
  vus: 1,
  iterations: 60,
  thresholds: {
    login_401: ['count>=1'],
    login_429: ['count>=1'],                 // el límite entró en acción
    checks: ['rate==1'],
  },
};

export default function () {
  const r = http.post(`${BASE_URL}/api/auth/login`, JSON.stringify({ correo: 'alumno@ejemplo.test', password: 'ClaveIncorrecta123' }), {
    headers: { 'Content-Type': 'application/json' },
    responseCallback: http.expectedStatuses(401, 429),
    tags: { tipo: 'escritura' },
  });
  if (r.status === 401) respuestas401.add(1);
  if (r.status === 429) {
    respuestas429.add(1);
    check(r, { '429 trae Retry-After': (x) => !!x.headers['Retry-After'], '429 es ProblemDetails': (x) => !!x.json('detail') });
  }
  check(r, { 'nunca 200 ni 5xx': (x) => x.status === 401 || x.status === 429 });
}

export const handleSummary = resumen('limite-auth');
