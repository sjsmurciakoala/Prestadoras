using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Mantenimientos;
using SIAD.Core.Entities;
using SIAD.Core.Utilities;
using SIAD.Data;

namespace SIAD.Services.Mantenimientos;

/// <summary>
/// Mantenimiento del catálogo de formatos fiscales (cfg_formato_fiscal): la máscara del
/// No. de factura (SAR) y del CAI que se transcriben del proveedor.
/// Multiempresa: el filtro y el estampado de company_id los aplica SiadDbContext.
/// El historial de cambios lo escribe solo el interceptor de la bitácora de maestros.
/// </summary>
public sealed class FormatoFiscalService : IFormatoFiscalService
{
    private const int MaxCodigo = 30;
    private const int MaxNombre = 60;
    private const int MaxMascara = 80;
    private const int MaxPatron = 200;

    private readonly SiadDbContext _context;

    public FormatoFiscalService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<FormatoFiscalListItemDto>> GetAsync(FormatoFiscalFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new FormatoFiscalFilterDto();
        var query = _context.cfg_formato_fiscals.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(f => f.activo == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";
            query = _context.Database.IsRelational()
                ? query.Where(f => EF.Functions.ILike(f.nombre, like) || EF.Functions.ILike(f.codigo, like))
                : query.Where(f => f.nombre.ToLower().Contains(term.ToLower()) || f.codigo.ToLower().Contains(term.ToLower()));
        }

        var filas = await query
            .OrderBy(f => f.nombre)
            .Select(f => new
            {
                f.id,
                f.codigo,
                f.nombre,
                f.mascara,
                f.modo_validacion,
                f.obligatorio,
                f.activo
            })
            .ToListAsync(ct);

        var resultado = new List<FormatoFiscalListItemDto>(filas.Count);
        foreach (var f in filas)
        {
            resultado.Add(new FormatoFiscalListItemDto
            {
                Id = f.id,
                Codigo = f.codigo,
                Nombre = f.nombre,
                Mascara = f.mascara,
                Ejemplo = FiscalCodeFormatter.Ejemplo(f.mascara),
                ModoValidacion = f.modo_validacion,
                Obligatorio = f.obligatorio,
                Activo = f.activo
            });
        }

        return resultado;
    }

    public async Task<FormatoFiscalEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        return await _context.cfg_formato_fiscals.AsNoTracking()
            .Where(f => f.id == id)
            .Select(f => new FormatoFiscalEditDto
            {
                Id = f.id,
                Codigo = f.codigo,
                Nombre = f.nombre,
                Mascara = f.mascara,
                Patron = f.patron,
                ModoValidacion = f.modo_validacion,
                Obligatorio = f.obligatorio,
                Normalizar = f.normalizar,
                Mayusculas = f.mayusculas,
                Activo = f.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<FormatoFiscalLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var filas = await _context.cfg_formato_fiscals.AsNoTracking()
            .Where(f => f.activo)
            .OrderBy(f => f.codigo)
            .Select(f => new
            {
                f.codigo,
                f.nombre,
                f.mascara,
                f.patron,
                f.modo_validacion,
                f.obligatorio,
                f.normalizar,
                f.mayusculas
            })
            .ToListAsync(ct);

        var resultado = new List<FormatoFiscalLookupDto>(filas.Count);
        foreach (var f in filas)
        {
            var patron = string.IsNullOrWhiteSpace(f.patron)
                ? FiscalCodeFormatter.ToRegex(f.mascara)
                : f.patron!.Trim();

            resultado.Add(new FormatoFiscalLookupDto
            {
                Codigo = f.codigo,
                Nombre = f.nombre,
                Mascara = f.mascara,
                MascaraDevExpress = FiscalCodeFormatter.ToDevExpressMask(f.mascara),
                Patron = patron,
                Ejemplo = FiscalCodeFormatter.Ejemplo(f.mascara),
                ModoValidacion = f.modo_validacion,
                Obligatorio = f.obligatorio,
                Normalizar = f.normalizar,
                Mayusculas = f.mayusculas
            });
        }

        return resultado;
    }

    public async Task<FormatoFiscalEditDto> CreateAsync(FormatoFiscalEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = NormalizarCodigo(dto.Codigo);
        var nombre = Requerido(dto.Nombre, MaxNombre, "nombre visible");
        var mascara = ValidarMascara(dto.Mascara);
        var patron = ValidarPatron(dto.Patron);
        ValidarModo(dto.ModoValidacion);

        if (await ExisteCodigoAsync(codigo, null, ct))
        {
            throw new InvalidOperationException($"Ya existe un formato con el código {codigo}.");
        }

        var entity = new cfg_formato_fiscal
        {
            codigo = codigo,
            nombre = nombre,
            mascara = mascara,
            patron = patron,
            modo_validacion = dto.ModoValidacion,
            obligatorio = dto.Obligatorio,
            normalizar = dto.Normalizar,
            mayusculas = dto.Mayusculas,
            activo = dto.Activo,
            usuariocreacion = Usuario(user),
            fechacreacion = Ahora()
        };

        _context.cfg_formato_fiscals.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.id;
        dto.Codigo = codigo;
        dto.Patron = patron;
        return dto;
    }

    public async Task<FormatoFiscalEditDto> UpdateAsync(int id, FormatoFiscalEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.cfg_formato_fiscals.FirstOrDefaultAsync(f => f.id == id, ct)
                     ?? throw new KeyNotFoundException("El formato fiscal no existe.");

        var codigo = NormalizarCodigo(dto.Codigo);
        var nombre = Requerido(dto.Nombre, MaxNombre, "nombre visible");
        var mascara = ValidarMascara(dto.Mascara);
        var patron = ValidarPatron(dto.Patron);
        ValidarModo(dto.ModoValidacion);

        if (await ExisteCodigoAsync(codigo, id, ct))
        {
            throw new InvalidOperationException($"Ya existe un formato con el código {codigo}.");
        }

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.mascara = mascara;
        entity.patron = patron;
        entity.modo_validacion = dto.ModoValidacion;
        entity.obligatorio = dto.Obligatorio;
        entity.normalizar = dto.Normalizar;
        entity.mayusculas = dto.Mayusculas;
        entity.activo = dto.Activo;
        entity.usuariomodificacion = Usuario(user);
        entity.fechamodificacion = Ahora();

        await _context.SaveChangesAsync(ct);

        dto.Id = entity.id;
        dto.Codigo = codigo;
        dto.Patron = patron;
        return dto;
    }

    public async Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.cfg_formato_fiscals.FirstOrDefaultAsync(f => f.id == id, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = Usuario(user);
        entity.fechamodificacion = Ahora();
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private Task<bool> ExisteCodigoAsync(string codigo, int? exceptoId, CancellationToken ct)
    {
        var codigoLower = codigo.ToLower();
        return _context.cfg_formato_fiscals.AsNoTracking()
            .AnyAsync(f => f.codigo.ToLower() == codigoLower && (exceptoId == null || f.id != exceptoId.Value), ct);
    }

    /// <summary>El código es una llave técnica: mayúsculas, sin espacios y solo letras, dígitos y guion bajo.</summary>
    private static string NormalizarCodigo(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException("El código del campo es obligatorio.");
        }

        var sb = new StringBuilder(valor.Length);
        foreach (var ch in valor.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToUpperInvariant(ch));
            }
            else if (ch is ' ' or '_' or '-')
            {
                sb.Append('_');
            }
        }

        var codigo = sb.ToString().Trim('_');
        if (codigo.Length == 0)
        {
            throw new InvalidOperationException("El código del campo debe tener al menos una letra o un dígito.");
        }

        if (codigo.Length > MaxCodigo)
        {
            throw new InvalidOperationException($"El código no puede superar los {MaxCodigo} caracteres.");
        }

        return codigo;
    }

    private static string ValidarMascara(string? valor)
    {
        var mascara = Requerido(valor, MaxMascara, "máscara");

        if (!FiscalCodeFormatter.TieneMetacaracteres(mascara))
        {
            throw new InvalidOperationException(
                "La máscara debe llevar al menos un '#' (dígito), 'X' (letra o dígito) o 'H' (hexadecimal).");
        }

        return mascara;
    }

    /// <summary>El patrón es opcional; si viene, tiene que compilar, si no se guardaría algo que nunca valida.</summary>
    private static string? ValidarPatron(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var patron = valor.Trim();
        if (patron.Length > MaxPatron)
        {
            throw new InvalidOperationException($"El patrón no puede superar los {MaxPatron} caracteres.");
        }

        try
        {
            _ = Regex.Match(string.Empty, patron, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("El patrón no es una expresión regular válida.");
        }

        return patron;
    }

    private static void ValidarModo(short modo)
    {
        if (!ModoValidacionFormatoFiscal.EsValido(modo))
        {
            throw new InvalidOperationException("Elija cómo debe validarse el campo.");
        }
    }

    private static string Requerido(string? valor, int maxLength, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException($"El {campo} es obligatorio.");
        }

        var trimmed = valor.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"El {campo} no puede superar los {maxLength} caracteres.");
        }

        return trimmed;
    }

    private static string Usuario(string? user) => string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();

    private static DateTime Ahora() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}
