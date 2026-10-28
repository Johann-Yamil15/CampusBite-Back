using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CampusBite.Api.Common.Filters;

// JY-Sprint4 27/10/2026: TEC-09. Lee los campos de los formularios (multipart/form-data) en cultura invariante.
// El proveedor de ASP.NET usa la cultura del servidor: en uno configurado en español de España "55.50" se leería
// como 5550. Así "precio=55.50" significa siempre 55.50, igual que en el JSON del resto de la API.
// Reemplaza al FormValueProviderFactory de MVC y conserva su manejo de errores (formulario dañado o muy grande -> 400).
public sealed class FormularioInvarianteValueProviderFactory : IValueProviderFactory
{
    public Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        var request = context.ActionContext.HttpContext.Request;
        return request.HasFormContentType ? AgregarAsync(context, request) : Task.CompletedTask;
    }

    private static async Task AgregarAsync(ValueProviderFactoryContext context, HttpRequest request) =>
        context.ValueProviders.Add(new FormValueProvider(BindingSource.Form, await LeerAsync(request), CultureInfo.InvariantCulture));

    private static async Task<IFormCollection> LeerAsync(HttpRequest request)
    {
        try
        {
            return await request.ReadFormAsync();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            throw new ValueProviderException($"No se pudo leer el formulario: {ex.Message}", ex);
        }
    }
}
