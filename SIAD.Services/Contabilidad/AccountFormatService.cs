using Microsoft.EntityFrameworkCore;
using SIAD.Core.Utilities;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

public sealed class AccountFormatService : IAccountFormatService
{
    private readonly SiadDbContext context;
    private AccountFormat? cached;

    public AccountFormatService(SiadDbContext context)
    {
        this.context = context;
    }

    public async Task<AccountFormat> GetFormatAsync(CancellationToken ct = default)
    {
        if (cached is not null)
        {
            return cached;
        }

        var config = await context.con_configuracion_sistemas
            .AsNoTracking()
            .Select(c => new { c.formato_cuentas, c.separador_codigo })
            .FirstOrDefaultAsync(ct);

        cached = config is null
            ? AccountFormat.Default
            : new AccountFormat(
                string.IsNullOrWhiteSpace(config.formato_cuentas) ? AccountCodeFormatter.DefaultMask : config.formato_cuentas,
                string.IsNullOrWhiteSpace(config.separador_codigo) ? AccountCodeFormatter.DefaultSeparator : config.separador_codigo);

        return cached;
    }
}
