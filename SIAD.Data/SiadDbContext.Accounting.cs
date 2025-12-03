using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

public partial class SiadDbContext
{
    public virtual DbSet<con_plan_cuenta> con_plan_cuentas { get; set; } = null!;

    public virtual DbSet<con_centro_costo> con_centro_costos { get; set; } = null!;

    public virtual DbSet<con_periodo_contable> con_periodo_contables { get; set; } = null!;

    public virtual DbSet<con_diario> con_diarios { get; set; } = null!;
    
    public virtual DbSet<con_empresa_configuracion> con_empresa_configuracions { get; set; } = null!;

    public virtual DbSet<con_configuracion_sistema> con_configuracion_sistemas { get; set; } = null!;

    public virtual DbSet<con_saldo_cuenta> con_saldo_cuentas { get; set; } = null!;

    public virtual DbSet<con_configuracion_balance> con_configuracion_balances { get; set; } = null!;

    public virtual DbSet<con_configuracion_linea_resultado> con_configuracion_linea_resultados { get; set; } = null!;

    public virtual DbSet<con_configuracion_estado_resultado> con_configuracion_estado_resultados { get; set; } = null!;

    public virtual DbSet<con_configuracion_correlativo> con_configuracion_correlativos { get; set; } = null!;

    public virtual DbSet<con_regla_integracion> con_regla_integracions { get; set; } = null!;

    public virtual DbSet<con_plantilla_poliza> con_plantilla_polizas { get; set; } = null!;

    public virtual DbSet<con_plantilla_poliza_linea> con_plantilla_poliza_lineas { get; set; } = null!;

    public virtual DbSet<con_poliza> con_polizas { get; set; } = null!;

    public virtual DbSet<con_poliza_linea> con_poliza_lineas { get; set; } = null!;

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<cfg_company>(entity =>
        {
            entity.HasKey(e => e.company_id);
            entity.ToTable("cfg_company", "public");
            entity.HasIndex(e => e.code).IsUnique();

            entity.Property(e => e.company_id).HasColumnName("company_id");
            entity.Property(e => e.code).HasMaxLength(20);
            entity.Property(e => e.commercial_name).HasMaxLength(200);
            entity.Property(e => e.legal_name).HasMaxLength(200);
            entity.Property(e => e.tax_id).HasMaxLength(30);
            entity.Property(e => e.email).HasMaxLength(120);
            entity.Property(e => e.phone).HasMaxLength(30);
            entity.Property(e => e.address).HasMaxLength(500);
            entity.Property(e => e.country_code).HasMaxLength(3).IsFixedLength();
            entity.Property(e => e.currency_code).HasMaxLength(3).IsFixedLength();
            entity.Property(e => e.timezone).HasMaxLength(60);
            entity.Property(e => e.status).HasMaxLength(20);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);
        });

        modelBuilder.Entity<con_empresa_configuracion>(entity =>
        {
            entity.HasKey(e => e.company_id);
            entity.ToTable("con_empresa_configuracion", "public");

            entity.Property(e => e.company_id).ValueGeneratedNever();
            entity.Property(e => e.tipo_empresa).HasMaxLength(60);
            entity.Property(e => e.id_fiscal_siglas).HasMaxLength(10);
            entity.Property(e => e.id_fiscal_valor).HasMaxLength(40);
            entity.Property(e => e.tamano).HasMaxLength(30);
            entity.Property(e => e.capital).HasMaxLength(30);
            entity.Property(e => e.fecha_constitucion).HasColumnType("date");
            entity.Property(e => e.contacto).HasMaxLength(120);
            entity.Property(e => e.direccion).HasMaxLength(500);
            entity.Property(e => e.telefonos).HasMaxLength(120);
            entity.Property(e => e.ciudad).HasMaxLength(120);
            entity.Property(e => e.pais).HasMaxLength(120);
            entity.Property(e => e.email).HasMaxLength(160);
            entity.Property(e => e.pagina_web).HasMaxLength(200);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne<cfg_company>()
                .WithOne()
                .HasForeignKey<con_empresa_configuracion>(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<con_plan_cuenta>(entity =>
        {
            entity.HasKey(e => e.account_id);
            entity.ToTable("con_plan_cuentas", "public");
            entity.HasIndex(e => new { e.company_id, e.code }).IsUnique();

            entity.Property(e => e.account_id).HasColumnName("account_id");
            entity.Property(e => e.account_type).HasMaxLength(30);
            entity.Property(e => e.category).HasMaxLength(30);
            entity.Property(e => e.code).HasMaxLength(30);
            entity.Property(e => e.currency_code).HasMaxLength(3).IsFixedLength();
            entity.Property(e => e.name).HasMaxLength(200);
            entity.Property(e => e.status).HasMaxLength(20);
            entity.Property(e => e.description).HasMaxLength(500);
            entity.Property(e => e.level).HasDefaultValue((short)1);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(d => d.parent_account)
                .WithMany(p => p.child_accounts)
                .HasForeignKey(d => d.parent_account_id)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<con_centro_costo>(entity =>
        {
            entity.HasKey(e => e.cost_center_id);
            entity.ToTable("con_centro_costo", "public");
            entity.HasIndex(e => new { e.company_id, e.code }).IsUnique();

            entity.Property(e => e.code).HasMaxLength(30);
            entity.Property(e => e.name).HasMaxLength(150);
            entity.Property(e => e.description).HasMaxLength(300);
            entity.Property(e => e.status).HasMaxLength(20);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);
        });

        modelBuilder.Entity<con_periodo_contable>(entity =>
        {
            entity.HasKey(e => e.period_id);
            entity.ToTable("con_periodo_contable", "public");
            entity.HasIndex(e => new { e.company_id, e.code }).IsUnique();

            entity.Property(e => e.code).HasMaxLength(20);
            entity.Property(e => e.name).HasMaxLength(80);
            entity.Property(e => e.status).HasMaxLength(20);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);
            entity.Property(e => e.closed_by).HasMaxLength(100);
        });

        modelBuilder.Entity<con_diario>(entity =>
        {
            entity.HasKey(e => e.journal_id);
            entity.ToTable("con_diario", "public");
            entity.HasIndex(e => new { e.company_id, e.code }).IsUnique();

            entity.Property(e => e.code).HasMaxLength(20);
            entity.Property(e => e.name).HasMaxLength(120);
            entity.Property(e => e.description).HasMaxLength(300);
            entity.Property(e => e.sequence_prefix).HasMaxLength(20);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);
        });

        modelBuilder.Entity<con_regla_integracion>(entity =>
        {
            entity.HasKey(e => e.regla_id);
            entity.ToTable("con_regla_integracion", "public");
            entity.HasIndex(e => new { e.company_id, e.module, e.scenario_code }).IsUnique();

            entity.Property(e => e.module).HasMaxLength(30);
            entity.Property(e => e.scenario_code).HasMaxLength(50);
            entity.Property(e => e.description).HasMaxLength(300);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(d => d.debit_account)
                .WithMany()
                .HasForeignKey(d => d.debit_account_id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.credit_account)
                .WithMany()
                .HasForeignKey(d => d.credit_account_id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.cost_center)
                .WithMany()
                .HasForeignKey(d => d.cost_center_id)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<con_plantilla_poliza>(entity =>
        {
            entity.HasKey(e => e.template_id);
            entity.ToTable("con_plantilla_poliza", "public");
            entity.HasIndex(e => new { e.company_id, e.name }).IsUnique();

            entity.Property(e => e.module).HasMaxLength(30);
            entity.Property(e => e.document_type).HasMaxLength(50);
            entity.Property(e => e.name).HasMaxLength(150);
            entity.Property(e => e.description).HasMaxLength(500);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);
        });

        modelBuilder.Entity<con_plantilla_poliza_linea>(entity =>
        {
            entity.HasKey(e => e.template_line_id);
            entity.ToTable("con_plantilla_poliza_linea", "public");
            entity.HasIndex(e => new { e.company_id, e.template_id, e.line_number }).IsUnique();

            entity.Property(e => e.description).HasMaxLength(300);
            entity.Property(e => e.debit_formula).HasMaxLength(200);
            entity.Property(e => e.credit_formula).HasMaxLength(200);

            entity.HasOne(d => d.template)
                .WithMany(p => p.lineas)
                .HasForeignKey(d => d.template_id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.account)
                .WithMany(p => p.plantilla_lineas)
                .HasForeignKey(d => d.account_id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.cost_center)
                .WithMany(p => p.plantilla_lineas)
                .HasForeignKey(d => d.cost_center_id)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<con_poliza>(entity =>
        {
            entity.HasKey(e => e.poliza_id);
            entity.ToTable("con_poliza", "public");
            entity.HasIndex(e => new { e.company_id, e.poliza_number }).IsUnique();

            entity.Property(e => e.module).HasMaxLength(30);
            entity.Property(e => e.document_type).HasMaxLength(50);
            entity.Property(e => e.document_number).HasMaxLength(50);
            entity.Property(e => e.poliza_number).HasMaxLength(50);
            entity.Property(e => e.description).HasMaxLength(500);
            entity.Property(e => e.status).HasMaxLength(20);
            entity.Property(e => e.source_reference).HasMaxLength(120);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(d => d.journal)
                .WithMany(p => p.polizas)
                .HasForeignKey(d => d.journal_id)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.period)
                .WithMany(p => p.polizas)
                .HasForeignKey(d => d.period_id)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.template)
                .WithMany(p => p.polizas)
                .HasForeignKey(d => d.template_id)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<con_poliza_linea>(entity =>
        {
            entity.HasKey(e => e.poliza_line_id);
            entity.ToTable("con_poliza_linea", "public");
            entity.HasIndex(e => new { e.company_id, e.poliza_id, e.line_number }).IsUnique();

            entity.Property(e => e.description).HasMaxLength(300);
            entity.Property(e => e.debit_amount).HasColumnType("numeric(18,2)");
            entity.Property(e => e.credit_amount).HasColumnType("numeric(18,2)");
            entity.Property(e => e.currency_code).HasMaxLength(3).IsFixedLength();
            entity.Property(e => e.source_document).HasMaxLength(120);

            entity.HasOne(d => d.poliza)
                .WithMany(p => p.lineas)
                .HasForeignKey(d => d.poliza_id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.account)
                .WithMany(p => p.poliza_lineas)
                .HasForeignKey(d => d.account_id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.cost_center)
                .WithMany(p => p.poliza_lineas)
                .HasForeignKey(d => d.cost_center_id)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<con_configuracion_sistema>(entity =>
        {
            entity.HasKey(e => e.config_id);
            entity.ToTable("con_configuracion_sistema", "public");
            entity.HasIndex(e => e.company_id).IsUnique();

            entity.Property(e => e.config_id).ValueGeneratedOnAdd();
            entity.Property(e => e.separador_codigo).HasMaxLength(1);
            entity.Property(e => e.formato_cuentas).HasMaxLength(50);
            entity.Property(e => e.formato_centros).HasMaxLength(50);
            entity.Property(e => e.symbol_saldo_acreedor).HasMaxLength(5);
            entity.Property(e => e.monto_maximo).HasColumnType("numeric(18,2)");
            entity.Property(e => e.frecuencia_depreciacion).HasMaxLength(20);
            entity.Property(e => e.codigo_cuenta_util_acumulada_historica).HasMaxLength(30);
            entity.Property(e => e.codigo_cuenta_util_ejercicio_historica).HasMaxLength(30);
            entity.Property(e => e.codigo_cuenta_perdida_acumulada_historica).HasMaxLength(30);
            entity.Property(e => e.codigo_cuenta_perdida_ejercicio_historica).HasMaxLength(30);
            entity.Property(e => e.codigo_cuenta_util_acumulada_inflacion).HasMaxLength(30);
            entity.Property(e => e.codigo_cuenta_util_ejercicio_inflacion).HasMaxLength(30);
            entity.Property(e => e.codigo_cuenta_perdida_acumulada_inflacion).HasMaxLength(30);
            entity.Property(e => e.codigo_cuenta_perdida_ejercicio_inflacion).HasMaxLength(30);
            entity.Property(e => e.titulo_estado_resultados).HasMaxLength(100);
            entity.Property(e => e.titulo_balance_general).HasMaxLength(100);
            entity.Property(e => e.descripcion_activo).HasMaxLength(100);
            entity.Property(e => e.descripcion_pasivo).HasMaxLength(100);
            entity.Property(e => e.descripcion_capital).HasMaxLength(100);
            entity.Property(e => e.descripcion_pasivo_capital).HasMaxLength(100);
            entity.Property(e => e.descripcion_orden).HasMaxLength(100);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<con_configuracion_estado_resultado>(entity =>
        {
            entity.HasKey(e => e.config_resultado_id);
            entity.ToTable("con_configuracion_estado_resultado", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo }).IsUnique();

            entity.Property(e => e.config_resultado_id).ValueGeneratedOnAdd();
            entity.Property(e => e.tipo).HasMaxLength(20);
            entity.Property(e => e.codigo).HasMaxLength(50);
            entity.Property(e => e.descripcion).HasMaxLength(200);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<con_configuracion_correlativo>(entity =>
        {
            entity.HasKey(e => e.config_correlativo_id);
            entity.ToTable("con_configuracion_correlativo", "public");
            entity.HasIndex(e => new { e.company_id, e.tipo }).IsUnique();

            entity.Property(e => e.config_correlativo_id).ValueGeneratedOnAdd();
            entity.Property(e => e.tipo).HasMaxLength(50);
            entity.Property(e => e.numerador).HasMaxLength(100);
            entity.Property(e => e.formato).HasMaxLength(100);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== NUEVAS ENTIDADES =====

        modelBuilder.Entity<con_saldo_cuenta>(entity =>
        {
            entity.HasKey(e => e.saldo_id);
            entity.ToTable("con_saldo_cuenta", "public");
            entity.HasIndex(e => new { e.company_id, e.periodo_id, e.codigo_cuenta, e.mes, e.tipo_transaccion }).IsUnique();

            entity.Property(e => e.saldo_id).ValueGeneratedOnAdd();
            entity.Property(e => e.codigo_cuenta).HasMaxLength(30).IsRequired();
            entity.Property(e => e.debitos).HasColumnType("numeric(28,2)").HasDefaultValue(0);
            entity.Property(e => e.creditos).HasColumnType("numeric(28,2)").HasDefaultValue(0);
            entity.Property(e => e.presupuesto).HasColumnType("numeric(28,2)").HasDefaultValue(0);

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.periodo)
                .WithMany()
                .HasForeignKey(e => e.periodo_id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.cuenta)
                .WithMany()
                .HasForeignKey(e => e.codigo_cuenta)
                .HasPrincipalKey(e => e.code)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<con_configuracion_balance>(entity =>
        {
            entity.HasKey(e => e.config_balance_id);
            entity.ToTable("con_configuracion_balance", "public");
            entity.HasIndex(e => new { e.company_id, e.periodo_id, e.numero_linea }).IsUnique();

            entity.Property(e => e.config_balance_id).ValueGeneratedOnAdd();
            entity.Property(e => e.codigo_cuenta).HasMaxLength(30);
            entity.Property(e => e.descripcion_cuenta).HasMaxLength(200);
            entity.Property(e => e.descripcion_linea).HasMaxLength(200);
            entity.Property(e => e.porcentaje_activo).HasColumnType("numeric(5,2)");
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.periodo)
                .WithMany()
                .HasForeignKey(e => e.periodo_id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.cuenta)
                .WithMany()
                .HasForeignKey(e => e.codigo_cuenta)
                .HasPrincipalKey(e => e.code)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<con_configuracion_linea_resultado>(entity =>
        {
            entity.HasKey(e => e.config_linea_id);
            entity.ToTable("con_configuracion_linea_resultado", "public");
            entity.HasIndex(e => new { e.company_id, e.periodo_id, e.numero_linea }).IsUnique();

            entity.Property(e => e.config_linea_id).ValueGeneratedOnAdd();
            entity.Property(e => e.codigo_cuenta).HasMaxLength(30);
            entity.Property(e => e.descripcion_linea).HasMaxLength(200);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.periodo)
                .WithMany()
                .HasForeignKey(e => e.periodo_id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.cuenta)
                .WithMany()
                .HasForeignKey(e => e.codigo_cuenta)
                .HasPrincipalKey(e => e.code)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
