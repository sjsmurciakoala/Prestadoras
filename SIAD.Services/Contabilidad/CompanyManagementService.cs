using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

public sealed class CompanyManagementService : ICompanyManagementService
{
    private readonly SiadDbContext _context;
    private readonly IConfiguracionSistemaService _configuracionService;

    public CompanyManagementService(SiadDbContext context, IConfiguracionSistemaService configuracionService)
    {
        _context = context;
        _configuracionService = configuracionService;
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

        try
        {
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

            // Inicializar configuración del sistema contable con periodo inicial
            await _configuracionService.InicializarConfiguracionPorDefectoAsync(
                nuevaEmpresa.company_id,
                tenantCompanyId,
                fechaConstitucion,
                ct);

            await transaction.CommitAsync(ct);

            return MapearDto(nuevaEmpresa, configuracion, tamano, capital, fechaConstitucion);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
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

    public async Task<CompanyCreationDto?> ObtenerAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            return null;
        }

        var empresa = await _context.cfg_companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        if (empresa is null)
        {
            return null;
        }

        // Obtener configuración ignorando filtros de query
        var configuracion = await _context.con_empresa_configuracions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        if (configuracion is null)
        {
            return null;
        }

        var tamano = ParseCompanySize(configuracion.tamano);
        var capital = ParseCompanyCapital(configuracion.capital);
        var fechaConstitucion = configuracion.fecha_constitucion?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;

        return MapearDto(empresa, configuracion, tamano, capital, fechaConstitucion);
    }

    public async Task<CompanyCreationDto> ActualizarAsync(long companyId, CompanyCreationDto dto, string usuario,
        CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de la empresa debe ser mayor que cero.");
        }

        ArgumentNullException.ThrowIfNull(dto);

        usuario = string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim();

        var empresa = await _context.cfg_companies
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        if (empresa is null)
        {
            throw new InvalidOperationException($"No existe una empresa con el ID {companyId}.");
        }

        var configuracion = await _context.con_empresa_configuracions
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        if (configuracion is null)
        {
            throw new InvalidOperationException("La configuración de la empresa no existe.");
        }

        var normalizedName = NormalizarNombre(dto.Descripcion);
        var taxId = ConstruirTaxId(dto);
        var fechaConstitucion = dto.FechaConstitucion
            ?? throw new InvalidOperationException("La fecha de constitución es obligatoria.");
        var tamano = dto.Tamano ?? throw new InvalidOperationException("El tamaño es obligatorio.");
        var capital = dto.Capital ?? throw new InvalidOperationException("El capital es obligatorio.");

        var now = DateTime.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            // Actualizar empresa
            empresa.commercial_name = normalizedName;
            empresa.legal_name = normalizedName;
            empresa.tax_id = taxId;
            empresa.email = Limpiar(dto.Email);
            empresa.phone = Limpiar(dto.Telefonos);
            empresa.address = Limpiar(dto.Direccion);
            empresa.status = dto.Activa ? "ACTIVE" : "INACTIVE";
            empresa.updated_at = now;
            empresa.updated_by = usuario;

            _context.cfg_companies.Update(empresa);

            // Actualizar configuración
            configuracion.tipo_empresa = Limpiar(dto.TipoEmpresa);
            configuracion.id_fiscal_siglas = Limpiar(dto.IdFiscalSiglas)?.ToUpperInvariant();
            configuracion.id_fiscal_valor = Limpiar(dto.IdFiscalValor);
            configuracion.tamano = MapearTamano(tamano);
            configuracion.capital = MapearCapital(capital);
            configuracion.fecha_constitucion = DateOnly.FromDateTime(fechaConstitucion.Date);
            configuracion.contacto = Limpiar(dto.Contacto);
            configuracion.direccion = Limpiar(dto.Direccion);
            configuracion.telefonos = Limpiar(dto.Telefonos);
            configuracion.pais = Limpiar(dto.Pais);
            configuracion.email = Limpiar(dto.Email);
            configuracion.pagina_web = Limpiar(dto.PaginaWeb);
            configuracion.updated_at = now;
            configuracion.updated_by = usuario;

            _context.con_empresa_configuracions.Update(configuracion);

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return MapearDto(empresa, configuracion, tamano, capital, fechaConstitucion);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
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

    private static CompanySizeType ParseCompanySize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CompanySizeType.Pequena;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "PEQUEÑA" or "PEQUENA" => CompanySizeType.Pequena,
            "MEDIANA" => CompanySizeType.Mediana,
            "GRAN CONTRIBUYENTE" => CompanySizeType.GranContribuyente,
            _ => CompanySizeType.Pequena
        };
    }

    private static CompanyCapitalType ParseCompanyCapital(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CompanyCapitalType.Privado;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "PRIVADO" => CompanyCapitalType.Privado,
            "OFICIAL" => CompanyCapitalType.Oficial,
            "MIXTO" => CompanyCapitalType.Mixto,
            _ => CompanyCapitalType.Privado
        };
    }

    public async Task<bool> GuardarLogoAsync(long companyId, byte[] logoBytes, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(logoBytes);

        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de la empresa debe ser mayor que cero.");
        }

        if (logoBytes.Length == 0)
        {
            throw new ArgumentException("El logo no puede estar vacío.", nameof(logoBytes));
        }

        if (logoBytes.Length > 5 * 1024 * 1024) // 5MB máximo
        {
            throw new ArgumentException("El logo no puede superar los 5MB.", nameof(logoBytes));
        }

        var now = DateTime.UtcNow;
        var mimeType = DetectarMimeType(logoBytes);

        // 1. Guardar en cfg_company (siempre existe, base del sistema)
        var empresa = await _context.cfg_companies
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        if (empresa is null)
        {
            throw new InvalidOperationException($"No existe una empresa con el ID {companyId}.");
        }

        empresa.logo = logoBytes;
        empresa.logo_mime = mimeType;
        empresa.updated_at = now;
        empresa.updated_by = usuario;

        // 2. Guardar también en con_empresa_configuracion si existe (módulo contabilidad)
        var configuracion = await _context.con_empresa_configuracions
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        if (configuracion is not null)
        {
            configuracion.logo = logoBytes;
            configuracion.logo_mime = mimeType;
            configuracion.updated_at = now;
            configuracion.updated_by = usuario;
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(byte[] logoBytes, string? contentType)?> ObtenerLogoAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de la empresa debe ser mayor que cero.");
        }

        // Leer logo desde cfg_company (fuente principal)
        var empresa = await _context.cfg_companies
            .Where(c => c.company_id == companyId)
            .Select(c => new { c.logo, c.logo_mime })
            .FirstOrDefaultAsync(ct);

        if (empresa is null || empresa.logo is null || empresa.logo.Length == 0)
        {
            return null;
        }

        var contentType = empresa.logo_mime ?? DetectarMimeType(empresa.logo);
        return (empresa.logo, contentType);
    }

    private static string DetectarMimeType(byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            return "application/octet-stream";
        }

        // PNG: 89 50 4E 47
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return "image/png";
        }

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // SVG: <svg o <?xml
        if (bytes[0] == 0x3C && (bytes[1] == 0x73 || bytes[1] == 0x3F))
        {
            return "image/svg+xml";
        }

        return "application/octet-stream";
    }
}
