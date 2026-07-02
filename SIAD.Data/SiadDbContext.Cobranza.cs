using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Extensión parcial para entidades de cobranza nuevas (post-scaffold).
// ConfigureCobranzaModel es llamado desde ConfigureSarComplianceModel
// en SiadDbContext.SarCompliance.cs.
public partial class SiadDbContext
{
    public virtual DbSet<cln_llamada_cobranza> cln_llamada_cobranzas { get; set; } = null!;
    public virtual DbSet<cln_nota_cobro> cln_nota_cobros { get; set; } = null!;
    public virtual DbSet<cln_corte_masivo_hdr> cln_corte_masivo_hdrs { get; set; } = null!;
    public virtual DbSet<cln_corte_masivo_dtl> cln_corte_masivo_dtls { get; set; } = null!;
    public virtual DbSet<cln_carta_cobro_hdr> cln_carta_cobro_hdrs { get; set; } = null!;
    public virtual DbSet<cln_carta_cobro_dtl> cln_carta_cobro_dtls { get; set; } = null!;
    public virtual DbSet<cln_cliente_estado_log> cln_cliente_estado_logs { get; set; } = null!;

    private void ConfigureCobranzaModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<cln_llamada_cobranza>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cln_llamada_cobranza_pkey");
            entity.ToTable("cln_llamada_cobranza", "public");
            entity.HasIndex(e => new { e.company_id, e.codigocliente },
                "ix_cln_llamada_cobranza_company_cliente");

            entity.Property(e => e.company_id);
            entity.Property(e => e.codigocliente).HasMaxLength(20);
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.numero_llamado).HasMaxLength(30);
            entity.Property(e => e.resultado).HasMaxLength(50);
            entity.Property(e => e.usuario).HasMaxLength(100);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<cln_nota_cobro>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cln_nota_cobro_pkey");
            entity.ToTable("cln_nota_cobro", "public");
            entity.HasIndex(e => new { e.company_id, e.correlativo },
                "uq_nota_cobro_correlativo").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.codigocliente },
                "ix_cln_nota_cobro_company_cliente");

            entity.Property(e => e.company_id);
            entity.Property(e => e.correlativo).HasMaxLength(20);
            entity.Property(e => e.codigocliente).HasMaxLength(20);
            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.monto).HasColumnType("numeric(18,2)");
            entity.Property(e => e.estado).HasMaxLength(20).HasDefaultValue("EMITIDA");
            entity.Property(e => e.usuario).HasMaxLength(100);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<cln_corte_masivo_hdr>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cln_corte_masivo_hdr_pkey");
            entity.ToTable("cln_corte_masivo_hdr", "public");
            entity.HasIndex(e => new { e.company_id, e.correlativo },
                "uq_corte_masivo_hdr_correlativo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_cln_corte_masivo_hdr_company");

            entity.Property(e => e.correlativo).HasMaxLength(20);
            entity.Property(e => e.criterio).HasMaxLength(30).IsRequired(false);
            entity.Property(e => e.barrio_codigo).HasMaxLength(20);
            entity.Property(e => e.valor_minimo).HasColumnType("numeric(18,2)");
            entity.Property(e => e.estado).HasMaxLength(20).HasDefaultValue("GENERADO");
            entity.Property(e => e.usuario).HasMaxLength(100);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fecha_generacion).HasColumnType("date");
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.dias_corte).HasDefaultValue(0);
        });

        modelBuilder.Entity<cln_corte_masivo_dtl>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cln_corte_masivo_dtl_pkey");
            entity.ToTable("cln_corte_masivo_dtl", "public");
            entity.HasIndex(e => e.hdr_id, "ix_cln_corte_masivo_dtl_hdr");
            entity.HasIndex(e => new { e.company_id, e.cliente_clave },
                "ix_cln_corte_masivo_dtl_cliente");

            entity.Property(e => e.cliente_clave).HasMaxLength(20);
            entity.Property(e => e.nombre_cliente).HasMaxLength(250);
            entity.Property(e => e.saldo_adeudado).HasColumnType("numeric(18,2)");
            entity.Property(e => e.pagado).HasDefaultValue(false);
            entity.Property(e => e.fecha_pago).HasColumnType("date");
            entity.Property(e => e.orden_id);
        });

        modelBuilder.Entity<cln_carta_cobro_hdr>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cln_carta_cobro_hdr_pkey");
            entity.ToTable("cln_carta_cobro_hdr", "public");
            entity.HasIndex(e => new { e.company_id, e.correlativo },
                "uq_carta_cobro_hdr_correlativo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_cln_carta_cobro_hdr_company");

            entity.Property(e => e.correlativo).HasMaxLength(20);
            entity.Property(e => e.criterio).HasMaxLength(60);
            entity.Property(e => e.usuario).HasMaxLength(100);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fecha_generacion).HasColumnType("date");
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<cln_carta_cobro_dtl>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cln_carta_cobro_dtl_pkey");
            entity.ToTable("cln_carta_cobro_dtl", "public");
            entity.HasIndex(e => e.hdr_id, "ix_cln_carta_cobro_dtl_hdr");
            entity.HasIndex(e => new { e.company_id, e.cliente_clave },
                "ix_cln_carta_cobro_dtl_cliente");

            entity.Property(e => e.cliente_clave).HasMaxLength(20);
            entity.Property(e => e.nombre_cliente).HasMaxLength(250);
            entity.Property(e => e.saldo).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<cln_cliente_estado_log>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cln_cliente_estado_log_pkey");
            entity.ToTable("cln_cliente_estado_log", "public");
            entity.HasIndex(e => new { e.company_id, e.codigocliente },
                "ix_cln_cliente_estado_log_company_cliente");
            entity.HasIndex(e => new { e.company_id, e.codigocliente, e.tipo },
                "ix_cln_cliente_estado_log_tipo");

            entity.Property(e => e.codigocliente).HasMaxLength(20);
            entity.Property(e => e.tipo).HasMaxLength(30);
            entity.Property(e => e.usuario).HasMaxLength(100);
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
        });
    }
}
