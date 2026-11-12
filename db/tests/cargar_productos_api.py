"""JY-Sprint4 05/11/2026: carga N productos de prueba en UNA cafetería por la API (POST /api/cafeterias/{id}/productos),
para probar el menú con miles de productos en un ambiente real (por ejemplo, Neon a través de la API de Azure).

Uso (el token va en una variable de entorno, no en la línea de comandos):
    set CAMPUSBITE_TOKEN=<token del encargado de esa cafetería o de un AdminSistema>
    (PowerShell: $env:CAMPUSBITE_TOKEN="...")
    python db/tests/cargar_productos_api.py --api https://<api>/api --cafeteria <id_cafeteria> --cantidad 1500

    # Para borrarlos después (solo los de prueba, que no tienen órdenes):
    python db/tests/cargar_productos_api.py --api https://<api>/api --cafeteria <id_cafeteria> --eliminar

- Los nombres llevan el prefijo "Prueba carga" y un número: se reconocen y se pueden borrar después.
- Se puede volver a correr: los que ya existen (409, nombre repetido en la cafetería) cuentan como "ya existían".
- Reintenta ante 429/5xx o cortes de red, con espera creciente. Solo usa la biblioteca estándar de Python.
"""
import argparse, json, os, sys, time, urllib.error, urllib.request, uuid
from concurrent.futures import ThreadPoolExecutor

PREFIJO = 'Prueba carga'


def pedir(metodo, url, token, cuerpo=None, tipo=None, intentos=5):
    for intento in range(intentos):
        req = urllib.request.Request(url, data=cuerpo, method=metodo, headers={'Authorization': f'Bearer {token}'})
        if tipo:
            req.add_header('Content-Type', tipo)
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                return r.status, json.loads(r.read() or b'null')
        except urllib.error.HTTPError as e:
            if e.code in (429, 500, 502, 503, 504) and intento < intentos - 1:
                time.sleep(2 ** intento)
                continue
            return e.code, json.loads(e.read() or b'null')
        except (urllib.error.URLError, TimeoutError):
            if intento == intentos - 1:
                raise
            time.sleep(2 ** intento)


def formulario(campos):
    # El alta es multipart/form-data (admite imagen); aquí solo se mandan los campos de texto
    limite = uuid.uuid4().hex
    partes = [f'--{limite}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n' for k, v in campos.items()]
    return ''.join(partes).encode() + f'--{limite}--\r\n'.encode(), f'multipart/form-data; boundary={limite}'


def producto(n):
    return {
        'nombre': f'{PREFIJO} {n:04d}',
        'descripcion': f'Producto de prueba número {n} para la carga de datos',
        'precio': f'{10 + (n % 190) + (n % 4) * 0.25:.2f}',      # con punto decimal: la API lo lee igual en cualquier idioma
        'disponible': 'true' if n % 10 else 'false',              # uno de cada diez queda no disponible
    }


def cargar(api, cafeteria, token, cantidad, hilos):
    resultado = {'creados': 0, 'ya existían': 0, 'errores': 0}
    def uno(n):
        cuerpo, tipo = formulario(producto(n))
        estado, respuesta = pedir('POST', f'{api}/cafeterias/{cafeteria}/productos', token, cuerpo, tipo)
        if estado == 201:
            return 'creados'
        if estado == 409:
            return 'ya existían'
        print(f'  {PREFIJO} {n:04d}: {estado} {respuesta}', file=sys.stderr)
        return 'errores'
    inicio = time.time()
    with ThreadPoolExecutor(hilos) as pool:
        for i, clave in enumerate(pool.map(uno, range(1, cantidad + 1)), 1):
            resultado[clave] += 1
            if i % 100 == 0 or i == cantidad:
                print(f'{i}/{cantidad}  {resultado}  ({time.time() - inicio:.0f} s)', flush=True)
    return resultado


def eliminar(api, cafeteria, token, hilos):
    # Funciona con el listado paginado ({items, total}) y con el anterior (arreglo con todos)
    ids, pagina = [], 1
    while True:
        url = f'{api}/cafeterias/{cafeteria}/productos?buscar={PREFIJO.replace(" ", "%20")}&tamanoPagina=100&pagina={pagina}'
        estado, respuesta = pedir('GET', url, token)
        if estado != 200:
            sys.exit(f'No se pudo listar: {estado} {respuesta}')
        items = respuesta['items'] if isinstance(respuesta, dict) else respuesta
        ids += [p['idProducto'] for p in items if p['nombre'].startswith(PREFIJO)]
        if isinstance(respuesta, list) or not items:
            break
        pagina += 1
    print(f'Borrando {len(ids)} productos de prueba...')
    with ThreadPoolExecutor(hilos) as pool:
        estados = list(pool.map(lambda i: pedir('DELETE', f'{api}/cafeterias/{cafeteria}/productos/{i}', token)[0], ids))
    print({e: estados.count(e) for e in set(estados)})


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('--api', required=True, help='URL base, p. ej. https://campusbite-back.azurewebsites.net/api')
    p.add_argument('--cafeteria', required=True, help='id (UUID) de la cafetería')
    p.add_argument('--cantidad', type=int, default=1500)
    p.add_argument('--hilos', type=int, default=8, help='peticiones simultáneas (no subir mucho en planes gratuitos)')
    p.add_argument('--eliminar', action='store_true', help='borra los productos de prueba en lugar de crearlos')
    a = p.parse_args()
    token = os.environ.get('CAMPUSBITE_TOKEN') or sys.exit('Define CAMPUSBITE_TOKEN con el token del encargado o de un AdminSistema.')
    api = a.api.rstrip('/')
    if a.eliminar:
        eliminar(api, a.cafeteria, token, a.hilos)
    else:
        print(cargar(api, a.cafeteria, token, a.cantidad, a.hilos))
