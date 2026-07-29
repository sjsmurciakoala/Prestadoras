using SIAD.Core.DTOs.Proveedores;

namespace SIAD.Services.Proveedores;

/// <summary>
/// Normaliza y valida la lista de contactos que llega en el upsert del proveedor,
/// y deriva los valores que se replican en las columnas legacy de prv_proveedores.
/// Vive fuera del servicio para poder probarse sin base de datos: CreateAsync abre
/// su propia transacción y es incompatible con el fixture de rollback de los tests.
/// </summary>
public static class ProveedorContactosNormalizer
{
    // prv_proveedores.telefono es varchar(20), más angosta que
    // prv_proveedor_contacto.telefono (varchar 30).
    private const int TelefonoLegacyMaxLength = 20;

    public sealed record LegacyContactoFields(string? NombreContacto, string? Telefono, string? Email);

    public static List<ProveedorContactoDto> Normalize(IReadOnlyList<ProveedorContactoDto>? source)
    {
        var contactos = new List<ProveedorContactoDto>();
        source ??= new List<ProveedorContactoDto>();

        for (var i = 0; i < source.Count; i++)
        {
            var fila = $"fila {i + 1}";
            var nombre = Trim(source[i].Nombre, 150, $"nombre del contacto en la {fila}");
            var cargo = Trim(source[i].Cargo, 100, $"cargo en la {fila}");
            var telefono = Trim(source[i].Telefono, 30, $"teléfono en la {fila}");
            var extension = Trim(source[i].Extension, 10, $"extensión en la {fila}");
            var celular = Trim(source[i].Celular, 30, $"celular en la {fila}");
            var email = Trim(source[i].Email, 150, $"email en la {fila}");
            var observaciones = Trim(source[i].Observaciones, 500, $"observaciones en la {fila}");

            var vacia = nombre is null && cargo is null && telefono is null && extension is null
                && celular is null && email is null && observaciones is null
                && source[i].TipoContactoId is null;

            if (vacia)
            {
                continue;
            }

            if (nombre is null)
            {
                throw new ArgumentException($"Debe indicar el nombre del contacto en la {fila}.", nameof(source));
            }

            if (email is not null && !EsEmailValido(email))
            {
                throw new ArgumentException($"El email de la {fila} no tiene un formato válido.", nameof(source));
            }

            contactos.Add(new ProveedorContactoDto
            {
                ProveedorContactoId = source[i].ProveedorContactoId,
                TipoContactoId = source[i].TipoContactoId is long t && t > 0 ? t : null,
                Nombre = nombre,
                Cargo = cargo,
                Telefono = telefono,
                Extension = extension,
                Celular = celular,
                Email = email,
                Observaciones = observaciones,
                Orden = contactos.Count + 1
            });
        }

        var duplicado = contactos
            .GroupBy(x => x.Nombre!.ToUpperInvariant(), StringComparer.Ordinal)
            .Any(g => g.Count() > 1);

        if (duplicado)
        {
            throw new ArgumentException("No puede repetir el mismo contacto en el proveedor.", nameof(source));
        }

        return contactos;
    }

    // prv_proveedores.telefono es varchar(20) y prv_proveedor_contacto.telefono es
    // varchar(30): un teléfono largo reventaría el UPDATE del proveedor. La columna
    // legacy es un espejo de compatibilidad, no la fuente de verdad — truncar ahí no
    // pierde nada, el valor completo vive en el contacto.
    public static LegacyContactoFields BuildLegacyFields(IReadOnlyList<ProveedorContactoDto> contactos)
    {
        var primero = contactos.FirstOrDefault();
        var telefono = primero?.Telefono;
        if (telefono is not null && telefono.Length > TelefonoLegacyMaxLength)
        {
            telefono = telefono[..TelefonoLegacyMaxLength];
        }

        return new LegacyContactoFields(primero?.Nombre, telefono, primero?.Email);
    }

    private static string? Trim(string? value, int maxLength, string campo)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is not null && normalized.Length > maxLength)
        {
            throw new ArgumentException($"El campo {campo} no puede superar {maxLength} caracteres.");
        }

        return normalized;
    }

    // Validación deliberadamente laxa: un arroba con algo antes y un punto después.
    // Rechazar direcciones raras pero válidas sería peor que dejar pasar una mala.
    private static bool EsEmailValido(string email)
    {
        var arroba = email.IndexOf('@');
        if (arroba <= 0 || arroba == email.Length - 1 || email.IndexOf('@', arroba + 1) >= 0)
        {
            return false;
        }

        var dominio = email[(arroba + 1)..];
        return dominio.Contains('.') && !dominio.StartsWith('.') && !dominio.EndsWith('.')
            && !email.Contains(' ');
    }
}
