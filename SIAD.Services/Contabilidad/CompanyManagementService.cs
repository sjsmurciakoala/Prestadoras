using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

public sealed class CompanyManagementService : ICompanyManagementService
{
    private readonly SiadDbContext _context;

    public CompanyManagementService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<CompanyCreationDto> CrearAsync(long tenantCompanyId, CompanyCreationDto dto, string usuario,
        CancellationToken ct = default)
    {
        if (tenantCompanyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tenantCompanyId),
                "El identificador de la empresa activa debe ser mayor que cero.");
        }

        ArgumentNullException.ThrowIfNull(dto);

        usuario = string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim();
        var tenantCompany = await _context.cfg_companies
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.company_id == tenantCompanyId, ct);

        if (tenantCompany is null)
        {
            throw new InvalidOperationException("La empresa activa seleccionada no es válida.");
        }

        var normalizedCode = NormalizarCodigo(dto.Codigo);
        var normalizedName = NormalizarNombre(dto.Descripcion);
        var taxId = ConstruirTaxId(dto);
        var fechaConstitucion = dto.FechaConstitucion
            ?? throw new InvalidOperationException("La fecha de constitución es obligatoria.");
        var tamano = dto.Tamano ?? throw new InvalidOperationException("El tamaño es obligatorio.");
        var capital = dto.Capital ?? throw new InvalidOperationException("El capital es obligatorio.");

        var codeExists = await _context.cfg_companies
            .AsNoTracking()
            .AnyAsync(c => c.code == normalizedCode, ct);

        if (codeExists)
        {
            throw new InvalidOperationException($"Ya existe una empresa con el código {normalizedCode}.");
        }

        var now = DateTime.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var nuevaEmpresa = new cfg_company
        {
            code = normalizedCode,
            commercial_name = normalizedName,
            legal_name = normalizedName,
            tax_id = taxId,
            email = Limpiar(dto.Email),
            phone = Limpiar(dto.Telefonos),
            address = Limpiar(dto.Direccion),
            country_code = NormalizarCountryCode(dto.Pais) ?? tenantCompany.country_code,
            currency_code = tenantCompany.currency_code,
            timezone = tenantCompany.timezone,
            status = dto.Activa ? "ACTIVE" : "INACTIVE",
            created_at = now,
            created_by = usuario
        };

        _context.cfg_companies.Add(nuevaEmpresa);
        await _context.SaveChangesAsync(ct);

        var configuracion = new con_empresa_configuracion
        {
            company_id = nuevaEmpresa.company_id,
            tipo_empresa = Limpiar(dto.TipoEmpresa),
            id_fiscal_siglas = Limpiar(dto.IdFiscalSiglas)?.ToUpperInvariant(),
            id_fiscal_valor = Limpiar(dto.IdFiscalValor),
            tamano = MapearTamano(tamano),
            capital = MapearCapital(capital),
            fecha_constitucion = DateOnly.FromDateTime(fechaConstitucion.Date),
            contacto = Limpiar(dto.Contacto),
            direccion = Limpiar(dto.Direccion),
            telefonos = Limpiar(dto.Telefonos),
            pais = Limpiar(dto.Pais),
            email = Limpiar(dto.Email),
            pagina_web = Limpiar(dto.PaginaWeb),
            created_at = now,
            created_by = usuario
        };

        _context.con_empresa_configuracions.Add(configuracion);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return MapearDto(nuevaEmpresa, configuracion, tamano, capital, fechaConstitucion);
    }

    private static string NormalizarCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new InvalidOperationException("El código es obligatorio.");
        }

        codigo = codigo.Trim().ToUpperInvariant();
        return codigo.Length <= 20 ? codigo : codigo[..20];
    }

    private static string NormalizarNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new InvalidOperationException("La descripción es obligatoria.");
        }

        nombre = nombre.Trim();
        return nombre.Length <= 200 ? nombre : nombre[..200];
    }

    private static string ConstruirTaxId(CompanyCreationDto dto)
    {
        var siglas = Limpiar(dto.IdFiscalSiglas)?.ToUpperInvariant();
        var valor = Limpiar(dto.IdFiscalValor);
        if (string.IsNullOrWhiteSpace(siglas) || string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException("El ID fiscal es obligatorio.");
        }

        var compuesto = $"{siglas}-{valor}";
        return compuesto.Length <= 30 ? compuesto : compuesto[..30];
    }

    private static string? NormalizarCountryCode(string? pais)
    {
        var limpio = Limpiar(pais);
        if (string.IsNullOrWhiteSpace(limpio))
        {
            return null;
        }

        var code = limpio.Length <= 3 ? limpio : limpio[..3];
        return code.ToUpperInvariant();
    }

    private static string MapearTamano(CompanySizeType tamano)
    {
        return tamano switch
        {
            CompanySizeType.Pequena => "PEQUENA",
            CompanySizeType.Mediana => "MEDIANA",
            CompanySizeType.GranContribuyente => "GRAN CONTRIBUYENTE",
            _ => throw new ArgumentOutOfRangeException(nameof(tamano), tamano, null)
        };
    }

    private static string MapearCapital(CompanyCapitalType capital)
    {
        return capital switch
        {
            CompanyCapitalType.Privado => "PRIVADO",
            CompanyCapitalType.Oficial => "OFICIAL",
            CompanyCapitalType.Mixto => "MIXTO",
            _ => throw new ArgumentOutOfRangeException(nameof(capital), capital, null)
        };
    }

    private static CompanyCreationDto MapearDto(cfg_company company, con_empresa_configuracion configuracion,
        CompanySizeType tamano, CompanyCapitalType capital, DateTime fechaConstitucion)
    {
        return new CompanyCreationDto
        {
            CompanyId = company.company_id,
            Codigo = company.code,
            Descripcion = company.commercial_name,
            TipoEmpresa = configuracion.tipo_empresa ?? string.Empty,
            IdFiscalSiglas = configuracion.id_fiscal_siglas ?? string.Empty,
            IdFiscalValor = configuracion.id_fiscal_valor ?? string.Empty,
            Tamano = tamano,
            Capital = capital,
            FechaConstitucion = fechaConstitucion,
            Activa = string.Equals(company.status, "ACTIVE", StringComparison.OrdinalIgnoreCase),
            Contacto = configuracion.contacto,
            Direccion = configuracion.direccion ?? string.Empty,
            Telefonos = configuracion.telefonos,
            Pais = configuracion.pais ?? company.country_code,
            Email = configuracion.email ?? company.email,
            PaginaWeb = configuracion.pagina_web
        };
    }

    private static string? Limpiar(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }
}
