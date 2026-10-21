// JY-Sprint3 20/10/2026: prueba de carga HTTP de los endpoints de cafeterías (TEC-08).
// Muchos usuarios virtuales a la vez, cada uno repitiendo una mezcla realista de peticiones durante N segundos.
// Al final imprime, por operación: peticiones, por segundo, latencia p50/p95/p99/máx y respuestas inesperadas.
//
//   dotnet run -c Release --project tests/CampusBite.Carga -- \
//     --url http://localhost:5167 --usuarios 100 --segundos 30 \
//     --admin-correo admin@ejemplo.test --admin-password <clave>
//
// Registra un alumno nuevo para obtener su token (POST /api/auth/registro) y entra como admin con las credenciales
// dadas. SOLO contra una base de pruebas: las ediciones cambian datos de verdad.
// Termina con código 1 si hubo errores 5xx o respuestas que no se esperaban.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

var opciones = LeerArgumentos(args);
var url = opciones.GetValueOrDefault("url", "http://localhost:5167").TrimEnd('/');
var usuarios = int.Parse(opciones.GetValueOrDefault("usuarios", "50"));
var segundos = int.Parse(opciones.GetValueOrDefault("segundos", "30"));

using var http = new HttpClient(new SocketsHttpHandler { MaxConnectionsPerServer = usuarios * 2 })
{
    BaseAddress = new Uri(url),
    Timeout = TimeSpan.FromSeconds(30)
};

// --- Sesiones -------------------------------------------------------------------------------------
var correoAlumno = $"carga{Guid.NewGuid():N}@carga.test";
var registro = await http.PostAsJsonAsync("/api/auth/registro",
    new { nombre = "Alumno Carga", correo = correoAlumno, password = "ClaveCarga123" });
registro.EnsureSuccessStatusCode();
var tokenAlumno = (await registro.Content.ReadFromJsonAsync<JsonNode>())!["token"]!.GetValue<string>();

var login = await http.PostAsJsonAsync("/api/auth/login",
    new { correo = opciones["admin-correo"], password = opciones["admin-password"] });
login.EnsureSuccessStatusCode();
var tokenAdmin = (await login.Content.ReadFromJsonAsync<JsonNode>())!["token"]!.GetValue<string>();

// --- Datos para las peticiones: ids reales y cuántas páginas hay --------------------------------------
var primera = await Pedir(HttpMethod.Get, "/api/cafeterias?tamano=100", tokenAlumno);
var paginaJson = JsonNode.Parse(await primera.Content.ReadAsStringAsync())!;
var totalCafeterias = paginaJson["total"]!.GetValue<int>();
var ids = paginaJson["elementos"]!.AsArray().Select(e => e!["idCafeteria"]!.GetValue<string>()).ToArray();
var paginas = Math.Max(1, (totalCafeterias + 19) / 20);

// Cafeterías propias de la prueba para editar (no se tocan las demás)
var editables = new List<string>();
for (var i = 0; i < 20; i++)
{
    var alta = await Pedir(HttpMethod.Post, "/api/cafeterias", tokenAdmin, Cuerpo($"Carga HTTP {Guid.NewGuid():N}"[..30], i % 2 == 0));
    if (alta.StatusCode != HttpStatusCode.Created) throw new Exception($"No se pudo crear una cafetería de prueba: {alta.StatusCode}");
    editables.Add(JsonNode.Parse(await alta.Content.ReadAsStringAsync())!["idCafeteria"]!.GetValue<string>());
}
Console.WriteLine($"API: {url} · {totalCafeterias:N0} cafeterías ({paginas:N0} páginas de 20) · {usuarios} usuarios simultáneos · {segundos} s");

// --- Carga ------------------------------------------------------------------------------------------
var medidas = new ConcurrentDictionary<string, ConcurrentBag<double>>();
var inesperadas = new ConcurrentDictionary<string, int>();
var fin = DateTime.UtcNow.AddSeconds(segundos);
var reloj = Stopwatch.StartNew();

await Task.WhenAll(Enumerable.Range(0, usuarios).Select(async u =>
{
    var azar = new Random(u);
    while (DateTime.UtcNow < fin)
    {
        var tiro = azar.Next(100);
        var (operacion, metodo, ruta, token, cuerpo, esperado) = tiro switch
        {
            < 60 => ("GET lista (página)", HttpMethod.Get, $"/api/cafeterias?pagina={azar.Next(1, paginas + 1)}&tamano=20", tokenAlumno, (object?)null, HttpStatusCode.OK),
            < 75 => ("GET lista (búsqueda)", HttpMethod.Get, $"/api/cafeterias?buscar={azar.Next(0, 10000):0000}", tokenAlumno, null, HttpStatusCode.OK),
            < 90 => ("GET detalle", HttpMethod.Get, $"/api/cafeterias/{ids[azar.Next(ids.Length)]}", tokenAlumno, null, HttpStatusCode.OK),
            _ => ("PUT editar (admin)", HttpMethod.Put, $"/api/cafeterias/{editables[azar.Next(editables.Count)]}", tokenAdmin, null, HttpStatusCode.OK)
        };
        if (metodo == HttpMethod.Put)
        {
            var id = ruta[(ruta.LastIndexOf('/') + 1)..];
            cuerpo = Cuerpo($"Carga HTTP {id[..8]}", azar.Next(2) == 0);
        }

        var inicio = Stopwatch.GetTimestamp();
        HttpStatusCode codigo;
        try
        {
            using var respuesta = await Pedir(metodo, ruta, token, cuerpo);
            await respuesta.Content.ReadAsByteArrayAsync();
            codigo = respuesta.StatusCode;
        }
        catch (Exception ex)
        {
            inesperadas.AddOrUpdate($"{operacion}: {ex.GetType().Name}", 1, (_, n) => n + 1);
            continue;
        }
        medidas.GetOrAdd(operacion, _ => []).Add(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
        if (codigo != esperado) inesperadas.AddOrUpdate($"{operacion}: HTTP {(int)codigo}", 1, (_, n) => n + 1);
    }
}));
reloj.Stop();

// --- Reporte ----------------------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine($"{"Operación",-22} {"Peticiones",11} {"Por seg.",9} {"p50 ms",8} {"p95 ms",8} {"p99 ms",8} {"máx ms",8}");
foreach (var (operacion, bolsa) in medidas.OrderBy(m => m.Key))
    Imprimir(operacion, bolsa.ToArray());
Imprimir("TOTAL", medidas.Values.SelectMany(b => b).ToArray());
Console.WriteLine();
if (inesperadas.IsEmpty) Console.WriteLine("Respuestas inesperadas: ninguna");
else foreach (var (que, n) in inesperadas.OrderByDescending(i => i.Value)) Console.WriteLine($"Respuesta inesperada: {n,7} × {que}");
return inesperadas.IsEmpty ? 0 : 1;

void Imprimir(string operacion, double[] ms)
{
    Array.Sort(ms);
    double P(double q) => ms.Length == 0 ? 0 : ms[Math.Min(ms.Length - 1, (int)Math.Ceiling(q * ms.Length) - 1)];
    Console.WriteLine($"{operacion,-22} {ms.Length,11:N0} {ms.Length / reloj.Elapsed.TotalSeconds,9:N0} {P(0.50),8:N1} {P(0.95),8:N1} {P(0.99),8:N1} {(ms.Length > 0 ? ms[^1] : 0),8:N1}");
}

Task<HttpResponseMessage> Pedir(HttpMethod metodo, string ruta, string token, object? cuerpo = null)
{
    var mensaje = new HttpRequestMessage(metodo, ruta);
    mensaje.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (cuerpo is not null) mensaje.Content = JsonContent.Create(cuerpo);
    return http.SendAsync(mensaje);
}

static object Cuerpo(string nombre, bool variante) => new
{
    nombre, ubicacion = "Edificio de carga", esDelCampus = true, puntoEntrega = variante ? "Ventanilla A" : "Ventanilla B",
    metodosPago = variante ? new[] { "Efectivo", "Tarjeta" } : new[] { "Transferencia" }
};

static Dictionary<string, string> LeerArgumentos(string[] args)
{
    var d = new Dictionary<string, string>();
    for (var i = 0; i + 1 < args.Length; i += 2) d[args[i].TrimStart('-')] = args[i + 1];
    return d;
}
