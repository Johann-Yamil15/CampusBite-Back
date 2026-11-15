using System.Security.Cryptography;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Auth;
using Npgsql;

namespace CampusBite.Api.Tests.Postgres;

// UsuarioRepository contra los procedimientos reales. Cada prueba usa correos únicos.
// A PROPÓSITO NO se prueba RestablecerPasswordAsync: en esta línea base (develop) llama a
// sp_restablecer_password(correo, hash), que cambia la contraseña con solo el correo (AUD-001). Una prueba que lo
// ejecutara "con éxito" legitimaría la vulnerabilidad; la corrección (por token) vive en la rama de AUD-001.
[Collection(ColeccionPostgres.Nombre)]
public class UsuarioRepositoryTests(PostgresDePrueba bd)
{
    private UsuarioRepository Repo() => new(bd.Contexto());

    private static string Correo() => $"repo_{Guid.NewGuid():N}@ejemplo.test";

    private static string HashToken() => Convert.ToHexStringLower(SHA256.HashData(RandomNumberGenerator.GetBytes(32)));

    [FactPostgres]
    public async Task Registrar_guarda_los_datos_y_ObtenerCredenciales_los_mapea()
    {
        var correo = Correo();

        var id = await Repo().RegistrarAsync("Ana López", correo, "$2a$11$hash.de.prueba.no.real.xxxxxxxxxxxxxx", null, "7711234567");

        var c = await Repo().ObtenerCredencialesAsync(correo.ToUpperInvariant());   // el correo es CITEXT
        Assert.NotNull(c);
        Assert.Equal(id, c.IdUsuario);
        Assert.Equal(RolUsuario.Alumno, c.IdRol);
        Assert.Equal("$2a$11$hash.de.prueba.no.real.xxxxxxxxxxxxxx", c.ContrasenaHash);
        Assert.True(c.Activo);
        Assert.False(c.CorreoVerificado);
        Assert.Equal(("Ana López", "7711234567"), await LeerNombreYTelefonoAsync(id));
    }

    private async Task<(string, string?)> LeerNombreYTelefonoAsync(Guid id)
    {
        await using var conexion = new NpgsqlConnection(bd.Conexion);
        await conexion.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT nombre, telefono FROM usuario WHERE id_usuario = @id", conexion);
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        Assert.True(await r.ReadAsync());
        return (r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1));
    }

    [FactPostgres]
    public async Task Las_credenciales_de_un_correo_que_no_existe_son_null()
    {
        Assert.Null(await Repo().ObtenerCredencialesAsync(Correo()));
    }

    [FactPostgres]
    public async Task Las_credenciales_reflejan_cuenta_inactiva_y_rol_real()
    {
        var c = await Repo().ObtenerCredencialesAsync("cafeteria@ejemplo.test");
        Assert.NotNull(c);
        Assert.Equal((PostgresDePrueba.Encargado, RolUsuario.AdminCafeteria, true, true), (c.IdUsuario, c.IdRol, c.Activo, c.CorreoVerificado));

        var correo = Correo();
        var id = await Repo().RegistrarAsync("Inactiva", correo, "HASH_INACTIVA_PRUEBA_X", null, null);
        await bd.EjecutarAsync("UPDATE usuario SET activo = false WHERE id_usuario = @id", new NpgsqlParameter("id", id));
        Assert.False((await Repo().ObtenerCredencialesAsync(correo))!.Activo);
    }

    [FactPostgres]
    public async Task Registrar_un_correo_o_matricula_repetidos_da_Conflict_con_mensaje_propio()
    {
        var correo = Correo();
        var matricula = "UTT" + Random.Shared.Next(1000000, 9999999);
        await Repo().RegistrarAsync("Primero", correo, "HASH_PRIMERO_PRUEBA_X", matricula, null);

        var porCorreo = await Assert.ThrowsAsync<ConflictException>(() =>
            Repo().RegistrarAsync("Segundo", correo.ToUpperInvariant(), "HASH_SEGUNDO_PRUEBA_X", null, null));
        var porMatricula = await Assert.ThrowsAsync<ConflictException>(() =>
            Repo().RegistrarAsync("Tercero", Correo(), "HASH_TERCERO_PRUEBA_X", matricula, null));

        Assert.Equal("El correo o la matrícula ya están registrados.", porCorreo.Message);
        Assert.Equal("El correo o la matrícula ya están registrados.", porMatricula.Message);
    }

    [FactPostgres]
    public async Task Registrar_datos_que_la_BD_rechaza_da_BusinessRule_con_mensaje_propio()
    {
        // ck de usuario.correo: debe tener @ y dominio con punto
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Repo().RegistrarAsync("Formato", "sin-arroba-" + Guid.NewGuid().ToString("N")[..6], "HASH_FORMATO_PRUEBA_X", null, null));

        Assert.Equal("Los datos no cumplen el formato requerido (nombre, correo o teléfono).", ex.Message);
    }

    [FactPostgres]
    public async Task Dos_registros_simultaneos_con_el_mismo_correo_solo_crean_uno()
    {
        var correo = Correo();

        var resultados = await Task.WhenAll(Enumerable.Range(0, 2).Select(async i =>
        {
            try { await Repo().RegistrarAsync($"Carrera {i}", correo, "HASH_CARRERA_PRUEBA_X", null, null); return "ok"; }
            catch (ConflictException) { return "conflicto"; }
        }));

        Assert.Equal(["conflicto", "ok"], resultados.Order());
        Assert.Equal(1L, await bd.EscalarAsync<long>("SELECT count(*) FROM usuario WHERE correo = @c::citext", new NpgsqlParameter("c", correo)));
    }

    [FactPostgres]
    public async Task Generar_y_verificar_correo_con_token_de_un_solo_uso()
    {
        var correo = Correo();
        var id = await Repo().RegistrarAsync("Verificable", correo, "HASH_VERIFICABLE_PRUEBA", null, null);
        var hash = HashToken();

        var nombre = await Repo().GenerarVerificacionAsync(correo, hash, DateTimeOffset.UtcNow.AddHours(24));
        var (idVerificado, correoVerificado, rol) = await Repo().VerificarCorreoAsync(hash);

        Assert.Equal("Verificable", nombre);
        Assert.Equal((id, correo, RolUsuario.Alumno), (idVerificado, correoVerificado, rol));
        Assert.True((await Repo().ObtenerCredencialesAsync(correo))!.CorreoVerificado);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Repo().VerificarCorreoAsync(hash));      // ya usado
    }

    [FactPostgres]
    public async Task Generar_verificacion_para_correo_inexistente_o_ya_verificado_devuelve_null()
    {
        Assert.Null(await Repo().GenerarVerificacionAsync(Correo(), HashToken(), DateTimeOffset.UtcNow.AddHours(24)));
        Assert.Null(await Repo().GenerarVerificacionAsync("alumno@ejemplo.test", HashToken(), DateTimeOffset.UtcNow.AddHours(24)));
    }

    [FactPostgres]
    public async Task Verificar_con_token_vencido_o_inventado_da_BusinessRule()
    {
        var correo = Correo();
        await Repo().RegistrarAsync("Vencido", correo, "HASH_VENCIDO_PRUEBA_X", null, null);
        var hash = HashToken();
        await Repo().GenerarVerificacionAsync(correo, hash, DateTimeOffset.UtcNow.AddHours(24));
        await bd.EjecutarAsync("UPDATE usuario SET verificacion_expira = now() - interval '1 minute' WHERE correo = @c::citext",
            new NpgsqlParameter("c", correo));

        var vencido = await Assert.ThrowsAsync<BusinessRuleException>(() => Repo().VerificarCorreoAsync(hash));
        Assert.Contains("venció", vencido.Message);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Repo().VerificarCorreoAsync(HashToken()));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Repo().GenerarVerificacionAsync(correo, "no-es-hash", DateTimeOffset.UtcNow.AddHours(1)));
    }

    [FactPostgres]
    public async Task RegistrarCafeteria_crea_encargado_y_cafeteria_enlazados()
    {
        var correo = Correo();
        var cafeteria = $"Negocio {Guid.NewGuid().ToString("N")[..8]}";

        var id = await Repo().RegistrarCafeteriaAsync("Dueña", correo, "HASH_DUENA_PRUEBA_XX", "7719998877",
            cafeteria, "Local 4", false, "Barra", [MetodoPago.Efectivo, MetodoPago.Tarjeta]);

        var c = await Repo().ObtenerCredencialesAsync(correo);
        Assert.NotNull(c);
        Assert.Equal((id, RolUsuario.AdminCafeteria), (c.IdUsuario, c.IdRol));
        Assert.Equal(1L, await bd.EscalarAsync<long>("""
            SELECT count(*) FROM usuario u JOIN cafeteria c ON c.id_cafeteria = u.id_cafeteria
             WHERE u.id_usuario = @id AND c.nombre = @n AND c.ubicacion = 'Local 4' AND NOT c.es_del_campus
               AND c.punto_entrega = 'Barra'
               AND (SELECT array_agg(metodo::text ORDER BY metodo::text) FROM cafeteria_metodo_pago m
                     WHERE m.id_cafeteria = c.id_cafeteria) = ARRAY['efectivo','tarjeta']
            """, new NpgsqlParameter("id", id), new NpgsqlParameter("n", cafeteria)));
    }

    [FactPostgres]
    public async Task RegistrarCafeteria_con_correo_o_nombre_repetido_da_Conflict_y_no_deja_nada_a_medias()
    {
        var cafeteria = $"Negocio {Guid.NewGuid().ToString("N")[..8]}";
        await Repo().RegistrarCafeteriaAsync("Original", Correo(), "HASH_ORIGINAL_PRUEBA", null, cafeteria, "L1", true, "P1",
            [MetodoPago.Efectivo]);
        var correo = Correo();

        var porNombre = await Assert.ThrowsAsync<ConflictException>(() => Repo().RegistrarCafeteriaAsync(
            "Copia", correo, "HASH_COPIA_PRUEBA_XX", null, cafeteria.ToUpperInvariant(), "L2", true, "P2", [MetodoPago.Efectivo]));
        var porCorreo = await Assert.ThrowsAsync<ConflictException>(() => Repo().RegistrarCafeteriaAsync(
            "Copia", "alumno@ejemplo.test", "HASH_COPIA_PRUEBA_XX", null, $"Otra {Guid.NewGuid():N}", "L2", true, "P2", [MetodoPago.Efectivo]));

        Assert.Contains("cafetería", porNombre.Message);
        Assert.Contains("correo", porCorreo.Message);
        Assert.Null(await Repo().ObtenerCredencialesAsync(correo));                  // la transacción no dejó el usuario
    }
}
