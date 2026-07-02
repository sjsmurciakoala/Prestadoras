using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Bancos;

public sealed class BancoConfiguracionService : IBancoConfiguracionService
{
    private readonly SiadDbContext dbContext;

    public BancoConfiguracionService(SiadDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<BancoConfiguracionDto> ObtenerAsync(long companyId, CancellationToken ct = default)
    {
        var entity = await dbContext.ban_config
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        return entity is null ? new BancoConfiguracionDto() : MapToDto(entity);
    }

    public async Task<BancoConfiguracionDto> GuardarAsync(long companyId, BancoConfiguracionDto dto, string user, CancellationToken ct = default)
    {
        var entity = await dbContext.ban_config
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        if (entity is null)
        {
            entity = new ban_config
            {
                company_id = companyId,
                created_at = DateTime.UtcNow,
                created_by = user
            };
            dbContext.ban_config.Add(entity);
        }

        MapToEntity(entity, dto, user);
        await dbContext.SaveChangesAsync(ct);
        return MapToDto(entity);
    }

    public async Task<IReadOnlyList<CuentaContableLookupDto>> ListarCuentasMayoresAsync(long companyId, CancellationToken ct = default)
    {
        return await dbContext.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId && !c.allows_posting)
            .OrderBy(c => c.code)
            .Select(c => new CuentaContableLookupDto
            {
                AccountId = c.account_id,
                Code = c.code,
                Description = c.name
            })
            .ToListAsync(ct);
    }

    private static BancoConfiguracionDto MapToDto(ban_config entity)
    {
        return new BancoConfiguracionDto
        {
            ConfigId = entity.ban_config_id,
            MaxCheque = entity.max_cheque,
            DiasD1 = entity.dias_d1,
            DiasD2 = entity.dias_d2,
            DiasD3 = entity.dias_d3,
            IChequeNe = entity.i_cheque_ne,
            ICEgreso = entity.i_c_egreso,
            PrxCEgreso = entity.prx_c_egreso,
            PrxDeposito = entity.prx_deposito,
            PrxNDebito = entity.prx_n_debito,
            PrxNCredito = entity.prx_n_credito,
            PDebBan = entity.p_deb_ban,
            MesesH = entity.meses_h,
            StDta = entity.st_dta,
            AlertarNd = entity.alertar_nd != 0,
            MOpeConc = entity.m_ope_conc,
            Consolidado = entity.consolidado,
            CuentaMayor = entity.cuenta_mayor,
            DirContab = entity.dir_contab,
            DirDtaCont = entity.dir_dta_cont,
            CcTipo = entity.cc_tipo,
            CcDescrip = entity.cc_descrip,
            CcSsw = entity.cc_ssw != 0,
            CcServer = entity.cc_server,
            CcDb = entity.cc_db,
            CcUser = entity.cc_user,
            CcPwd = entity.cc_pwd,
            CcPrefix = entity.cc_prefix,
            NroCxb = entity.nro_cxb,
            ACtas0 = entity.a_ctas0,
            ACtas1 = entity.a_ctas1,
            ACtas2 = entity.a_ctas2,
            ACtas3 = entity.a_ctas3,
            ACtas4 = entity.a_ctas4,
            ACtas5 = entity.a_ctas5,
            NOpe1 = entity.n_ope1,
            NOpe2 = entity.n_ope2,
            NOpe3 = entity.n_ope3,
            NOpe4 = entity.n_ope4,
            NOpe5 = entity.n_ope5,
            NOpe6 = entity.n_ope6,
            NOpe7 = entity.n_ope7,
            NOpe8 = entity.n_ope8,
            NOpe9 = entity.n_ope9,
            NOpe10 = entity.n_ope10,
            CtaAux1 = entity.cta_aux1,
            CtaAux2 = entity.cta_aux2,
            CtaAux3 = entity.cta_aux3,
            CodSucu = entity.cod_sucu
        };
    }

    private static void MapToEntity(ban_config entity, BancoConfiguracionDto dto, string user)
    {
        entity.max_cheque = dto.MaxCheque;
        entity.dias_d1 = dto.DiasD1;
        entity.dias_d2 = dto.DiasD2;
        entity.dias_d3 = dto.DiasD3;
        entity.i_cheque_ne = dto.IChequeNe;
        entity.i_c_egreso = dto.ICEgreso;
        entity.prx_c_egreso = dto.PrxCEgreso;
        entity.prx_deposito = dto.PrxDeposito;
        entity.prx_n_debito = dto.PrxNDebito;
        entity.prx_n_credito = dto.PrxNCredito;
        entity.p_deb_ban = dto.PDebBan;
        entity.meses_h = dto.MesesH;
        entity.st_dta = dto.StDta;
        entity.alertar_nd = dto.AlertarNd ? 1 : 0;
        entity.m_ope_conc = dto.MOpeConc;
        entity.consolidado = dto.Consolidado;
        entity.cuenta_mayor = dto.CuentaMayor;
        entity.dir_contab = dto.DirContab;
        entity.dir_dta_cont = dto.DirDtaCont;
        entity.cc_tipo = dto.CcTipo;
        entity.cc_descrip = dto.CcDescrip;
        entity.cc_ssw = dto.CcSsw ? 1 : 0;
        entity.cc_server = dto.CcServer;
        entity.cc_db = dto.CcDb;
        entity.cc_user = dto.CcUser;
        entity.cc_pwd = dto.CcPwd;
        entity.cc_prefix = dto.CcPrefix;
        entity.nro_cxb = dto.NroCxb;
        entity.a_ctas0 = dto.ACtas0;
        entity.a_ctas1 = dto.ACtas1;
        entity.a_ctas2 = dto.ACtas2;
        entity.a_ctas3 = dto.ACtas3;
        entity.a_ctas4 = dto.ACtas4;
        entity.a_ctas5 = dto.ACtas5;
        entity.n_ope1 = dto.NOpe1;
        entity.n_ope2 = dto.NOpe2;
        entity.n_ope3 = dto.NOpe3;
        entity.n_ope4 = dto.NOpe4;
        entity.n_ope5 = dto.NOpe5;
        entity.n_ope6 = dto.NOpe6;
        entity.n_ope7 = dto.NOpe7;
        entity.n_ope8 = dto.NOpe8;
        entity.n_ope9 = dto.NOpe9;
        entity.n_ope10 = dto.NOpe10;
        entity.cta_aux1 = dto.CtaAux1;
        entity.cta_aux2 = dto.CtaAux2;
        entity.cta_aux3 = dto.CtaAux3;
        entity.cod_sucu = dto.CodSucu;
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = user;
    }
}
