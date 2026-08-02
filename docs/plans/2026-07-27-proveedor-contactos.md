# Contactos de proveedor — plan de implementación

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Un proveedor puede tener N contactos (opcionales), cada uno con su tipo tomado de un catálogo con mantenimiento propio.

**Architecture:** Dos tablas nuevas en Postgres — `prv_tipo_contacto` (catálogo por empresa) y `prv_proveedor_contacto` (hija del proveedor). Los contactos viajan dentro del upsert del proveedor y se sincronizan con un diff por id, exactamente como ya se hace con las cuentas bancarias. El primer contacto se replica en las columnas legacy de `prv_proveedores` para no romper reportes ni consultas existentes.

**Tech Stack:** .NET 9, EF Core 9 + Npgsql, Blazor WASM con DevExpress 25.1.7, xUnit.

**Diseño aprobado:** [2026-07-27-proveedor-contactos-design.md](2026-07-27-proveedor-contactos-design.md)

---

## Convenciones de este plan

- Ningún commit automático. Si el usuario los pide, se agregan al final.
- Ningún script SQL se aplica contra una base de datos. Se crea el archivo y se registra en el runbook; aplicarlo es decisión del usuario.
- Comando de build: `dotnet build HODSOFT_DEVEXPRESS.sln`
- Comando de tests: `$env:SIAD_TEST_DB = '<conn>'; dotnet test SIAD.Tests/SIAD.Tests.csproj`

---

## Task 1: Script SQL de las dos tablas

**REQUIRED SUB-SKILL:** usar `guardia-estructura-bd` antes de crear el archivo (cambio aditivo → tarjeta verde) y `runbook-despliegue-srv` después de crearlo.

**Files:**
- Create: `Database/2026-07-27_proveedor_contactos.sql`
- Modify: `Database/2026-07-23_runbook_despliegue_srv.md` (o el runbook vigente más reciente)

**Step 1: Escribir el script**

Plantilla exacta a seguir: `Database/2026-07-17_prv_compromiso_abono.sql` (encabezado explicativo, `BEGIN/COMMIT`, `COMMENT ON`, bloque de verificación comentado al final).

```sql
-- =============================================================================
-- Proveedores: contactos por proveedor + catálogo de tipos de contacto
-- Fecha: 2026-07-27
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en SRV
--
-- POR QUÉ ESTAS TABLAS
-- Un proveedor tenía UN solo contacto, guardado como columnas sueltas en
-- prv_proveedores (nombre_contacto, telefono, email). En la práctica un proveedor
-- tiene varias personas — ventas, cobros, soporte — y no había dónde registrarlas.
--
-- COMPANY_ID EN LA TABLA HIJA
-- El correlativo del proveedor se genera por empresa, así que cod_proveedor se
-- REPITE entre empresas. Colgar los contactos solo de cod_proveedor los volvería
-- visibles entre tenants. Por eso company_id va en la hija y las entidades
-- implementan ICompanyScopedEntity (query filter global de SiadDbContext).
--
-- SIN FK A prv_proveedores: esa tabla no declara PK (entidad keyless en EF), así
-- que no hay a qué apuntar. Mismo caso que prv_proveedor_cuenta_bancaria.
--
-- CAMPOS LEGACY: prv_proveedores.nombre_contacto/telefono/email NO se tocan. El
-- servicio los mantiene sincronizados con el contacto de orden = 1.
--
-- Cambio aditivo: dos tablas nuevas. No altera ninguna tabla ni dato existente.
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS prv_tipo_contacto (
    tipo_contacto_id   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         BIGINT       NOT NULL,
    nombre             VARCHAR(60)  NOT NULL,
    observaciones      VARCHAR(250),
    activo             BOOLEAN      NOT NULL DEFAULT TRUE,
    fecha_creacion     TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    usuario_creo       VARCHAR(100) NOT NULL,
    fecha_modificacion TIMESTAMP WITHOUT TIME ZONE,
    usuario_modifica   VARCHAR(100),
    rowid              UUID         NOT NULL DEFAULT gen_random_uuid()
);

-- Nombre único por empresa, sin distinguir mayúsculas ni espacios al borde.
CREATE UNIQUE INDEX IF NOT EXISTS uq_prv_tipo_contacto_nombre
    ON prv_tipo_contacto (company_id, upper(btrim(nombre)));

CREATE TABLE IF NOT EXISTS prv_proveedor_contacto (
    proveedor_contacto_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id            BIGINT       NOT NULL,
    cod_proveedor         VARCHAR(20)  NOT NULL,
    tipo_contacto_id      BIGINT,
    nombre                VARCHAR(150) NOT NULL,
    cargo                 VARCHAR(100),
    telefono              VARCHAR(30),
    extension             VARCHAR(10),
    celular               VARCHAR(30),
    email                 VARCHAR(150),
    observaciones         VARCHAR(500),
    orden                 INT          NOT NULL DEFAULT 1,
    fecha_creacion        TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT now(),
    usuario_creo          VARCHAR(100) NOT NULL,
    fecha_modificacion    TIMESTAMP WITHOUT TIME ZONE,
    usuario_modifica      VARCHAR(100),
    rowid                 UUID         NOT NULL DEFAULT gen_random_uuid(),

    -- RESTRICT: no se borra un tipo que esté asignado a algún contacto.
    -- El servicio da el mensaje amigable; esto es defensa en profundidad.
    CONSTRAINT fk_prv_proveedor_contacto_tipo
        FOREIGN KEY (tipo_contacto_id)
        REFERENCES prv_tipo_contacto (tipo_contacto_id)
        ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_prv_proveedor_contacto_proveedor
    ON prv_proveedor_contacto (company_id, cod_proveedor, orden);

COMMENT ON TABLE  prv_tipo_contacto IS
    'Catálogo de tipos de contacto de proveedor (Ventas, Cobros, ...), por empresa.';
COMMENT ON COLUMN prv_tipo_contacto.activo IS
    'FALSE retira el tipo de los combos sin borrarlo ni afectar contactos ya asignados.';
COMMENT ON TABLE  prv_proveedor_contacto IS
    'Contactos de un proveedor. Sin FK a prv_proveedores porque esa tabla no declara PK. El contacto de orden=1 se replica en prv_proveedores.nombre_contacto/telefono/email.';
COMMENT ON COLUMN prv_proveedor_contacto.company_id IS
    'Obligatorio: cod_proveedor se repite entre empresas (correlativo por empresa).';
COMMENT ON COLUMN prv_proveedor_contacto.orden IS
    'Posición en el grid del formulario. El orden 1 alimenta los campos legacy del proveedor.';
COMMENT ON COLUMN prv_proveedor_contacto.tipo_contacto_id IS
    'Opcional. NULL = contacto sin clasificar.';

-- Semilla del catálogo: una fila por empresa que ya tenga proveedores.
INSERT INTO prv_tipo_contacto (company_id, nombre, usuario_creo)
SELECT c.company_id, t.nombre, 'system'
FROM (SELECT DISTINCT company_id FROM prv_proveedores) c
CROSS JOIN (VALUES ('Ventas'), ('Cobros'), ('Gerencia'), ('Soporte técnico'), ('Administración')) AS t(nombre)
WHERE NOT EXISTS (
    SELECT 1 FROM prv_tipo_contacto x
    WHERE x.company_id = c.company_id
      AND upper(btrim(x.nombre)) = upper(btrim(t.nombre))
);

-- Migración: el contacto que hoy vive en las columnas sueltas pasa a ser el #1.
-- Idempotente: no inserta si el proveedor ya tiene contactos.
INSERT INTO prv_proveedor_contacto
    (company_id, cod_proveedor, nombre, telefono, email, orden, fecha_creacion, usuario_creo)
SELECT p.company_id,
       p.cod_proveedor,
       btrim(p.nombre_contacto),
       NULLIF(btrim(COALESCE(p.telefono, '')), ''),
       NULLIF(btrim(COALESCE(p.email, '')), ''),
       1,
       COALESCE(p.fecha_modificacion, p.fecha_creacion, now()),
       'migracion'
FROM prv_proveedores p
WHERE btrim(COALESCE(p.nombre_contacto, '')) <> ''
  AND NOT EXISTS (
      SELECT 1 FROM prv_proveedor_contacto d
      WHERE d.company_id = p.company_id
        AND d.cod_proveedor = p.cod_proveedor
  );

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (correr a mano tras aplicar)
-- =============================================================================
-- 1) Columnas:
-- SELECT column_name, data_type, character_maximum_length, is_nullable
--   FROM information_schema.columns WHERE table_name='prv_proveedor_contacto' ORDER BY ordinal_position;
-- 2) Constraints e índices:
-- SELECT conname, contype FROM pg_constraint WHERE conrelid='prv_proveedor_contacto'::regclass ORDER BY contype;
--   -> fk_prv_proveedor_contacto_tipo(f), prv_proveedor_contacto_pkey(p)
-- SELECT indexname FROM pg_indexes WHERE tablename IN ('prv_proveedor_contacto','prv_tipo_contacto') ORDER BY 1;
--   -> ix_prv_proveedor_contacto_proveedor, uq_prv_tipo_contacto_nombre, *_pkey
-- 3) Semilla y migración:
-- SELECT company_id, count(*) FROM prv_tipo_contacto GROUP BY 1;          -- 5 por empresa
-- SELECT count(*) FROM prv_proveedor_contacto;                            -- = proveedores con nombre_contacto
-- SELECT count(*) FROM prv_proveedores WHERE btrim(COALESCE(nombre_contacto,'')) <> '';  -- debe coincidir
-- 4) El nombre duplicado por empresa debe FALLAR:
-- INSERT INTO prv_tipo_contacto (company_id, nombre, usuario_creo) VALUES (2, ' ventas ', 'test');
--   -> ERROR duplicate key value violates unique constraint "uq_prv_tipo_contacto_nombre"
-- =============================================================================
```

**Step 2: Registrar en el runbook**

Invocar la skill `runbook-despliegue-srv` y agregar la entrada del script. No aplicar nada.

---

## Task 2: Entidades EF y configuración del contexto

**Files:**
- Create: `SIAD.Core/Entities/prv_tipo_contacto.cs`
- Create: `SIAD.Core/Entities/prv_proveedor_contacto.cs`
- Modify: `SIAD.Data/SiadDbContext.cs` (DbSets junto a la línea 222; configuración junto a `prv_proveedor_cuenta_bancaria`, línea 1872)

**Step 1: Crear las entidades**

`SIAD.Core/Entities/prv_tipo_contacto.cs`:

```csharp
using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class prv_tipo_contacto : ICompanyScopedEntity
{
    public long tipo_contacto_id { get; set; }

    public long company_id { get; set; }

    public string nombre { get; set; } = null!;

    public string? observaciones { get; set; }

    public bool activo { get; set; } = true;

    public DateTime fecha_creacion { get; set; }

    public string usuario_creo { get; set; } = null!;

    public DateTime? fecha_modificacion { get; set; }

    public string? usuario_modifica { get; set; }

    public Guid? rowid { get; set; }
}
```

`SIAD.Core/Entities/prv_proveedor_contacto.cs`:

```csharp
using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class prv_proveedor_contacto : ICompanyScopedEntity
{
    public long proveedor_contacto_id { get; set; }

    public long company_id { get; set; }

    public string cod_proveedor { get; set; } = null!;

    public long? tipo_contacto_id { get; set; }

    public string nombre { get; set; } = null!;

    public string? cargo { get; set; }

    public string? telefono { get; set; }

    public string? extension { get; set; }

    public string? celular { get; set; }

    public string? email { get; set; }

    public string? observaciones { get; set; }

    public int orden { get; set; }

    public DateTime fecha_creacion { get; set; }

    public string usuario_creo { get; set; } = null!;

    public DateTime? fecha_modificacion { get; set; }

    public string? usuario_modifica { get; set; }

    public Guid? rowid { get; set; }
}
```

Al implementar `ICompanyScopedEntity`, `SiadDbContext.Tenancy.cs` aplica solo el filtro global y estampa `company_id` en los inserts. No hay que filtrar por empresa a mano en las consultas.

**Step 2: DbSets**

En `SIAD.Data/SiadDbContext.cs`, junto a la línea 222:

```csharp
    public virtual DbSet<prv_proveedor_contacto> prv_proveedor_contactos { get; set; }

    public virtual DbSet<prv_tipo_contacto> prv_tipo_contactos { get; set; }
```

**Step 3: Configuración del modelo**

En `OnModelCreating`, inmediatamente después del bloque de `prv_proveedor_cuenta_bancaria` (línea 1892):

```csharp
        modelBuilder.Entity<prv_tipo_contacto>(entity =>
        {
            entity.HasKey(e => e.tipo_contacto_id);

            entity.ToTable("prv_tipo_contacto");

            entity.Property(e => e.tipo_contacto_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.fecha_creacion)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.fecha_modificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.nombre).HasMaxLength(60);
            entity.Property(e => e.observaciones).HasMaxLength(250);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.usuario_creo).HasMaxLength(100);
            entity.Property(e => e.usuario_modifica).HasMaxLength(100);
        });

        modelBuilder.Entity<prv_proveedor_contacto>(entity =>
        {
            entity.HasKey(e => e.proveedor_contacto_id);

            entity.ToTable("prv_proveedor_contacto");

            entity.HasIndex(e => new { e.company_id, e.cod_proveedor, e.orden })
                .HasDatabaseName("ix_prv_proveedor_contacto_proveedor");

            entity.Property(e => e.proveedor_contacto_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.cargo).HasMaxLength(100);
            entity.Property(e => e.celular).HasMaxLength(30);
            entity.Property(e => e.cod_proveedor).HasMaxLength(20);
            entity.Property(e => e.email).HasMaxLength(150);
            entity.Property(e => e.extension).HasMaxLength(10);
            entity.Property(e => e.fecha_creacion)
                .HasDefaultValueSql("now()")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.fecha_modificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.observaciones).HasMaxLength(500);
            entity.Property(e => e.orden).HasDefaultValue(1);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.telefono).HasMaxLength(30);
            entity.Property(e => e.usuario_creo).HasMaxLength(100);
            entity.Property(e => e.usuario_modifica).HasMaxLength(100);
        });
```

**Step 4: Compilar**

Run: `dotnet build HODSOFT_DEVEXPRESS.sln`
Expected: build sin errores.

---

## Task 3: DTOs

**Files:**
- Create: `SIAD.Core/DTOs/Proveedores/ProveedorContactoDto.cs`
- Create: `SIAD.Core/DTOs/Proveedores/TipoContactoDtos.cs`
- Modify: `SIAD.Core/DTOs/Proveedores/ProveedorUpsertDto.cs`
- Modify: `SIAD.Core/DTOs/Proveedores/ProveedorDetailDto.cs`

**Step 1: `ProveedorContactoDto`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Proveedores;

public sealed class ProveedorContactoDto
{
    public long? ProveedorContactoId { get; set; }

    public long? TipoContactoId { get; set; }

    /// <summary>Solo lectura: nombre del tipo resuelto para mostrar en el detalle.</summary>
    public string? TipoContacto { get; set; }

    [StringLength(150, ErrorMessage = "El nombre del contacto no puede superar 150 caracteres.")]
    public string? Nombre { get; set; }

    [StringLength(100, ErrorMessage = "El cargo no puede superar 100 caracteres.")]
    public string? Cargo { get; set; }

    [StringLength(30, ErrorMessage = "El teléfono no puede superar 30 caracteres.")]
    public string? Telefono { get; set; }

    [StringLength(10, ErrorMessage = "La extensión no puede superar 10 caracteres.")]
    public string? Extension { get; set; }

    [StringLength(30, ErrorMessage = "El celular no puede superar 30 caracteres.")]
    public string? Celular { get; set; }

    [StringLength(150, ErrorMessage = "El email no puede superar 150 caracteres.")]
    public string? Email { get; set; }

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar 500 caracteres.")]
    public string? Observaciones { get; set; }

    public int Orden { get; set; }
}
```

`Nombre` no lleva `[Required]`: una fila en blanco se descarta en la normalización, igual que hace hoy el grid de cuentas bancarias. La obligatoriedad se valida en el servicio solo para filas con algún dato.

**Step 2: DTOs del catálogo** — `TipoContactoDtos.cs`, calcados de `TipoProveedor*`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Proveedores;

public record TipoContactoLookupDto(long Id, string Nombre);

public record TipoContactoListItemDto(long Id, string Nombre, string? Observaciones, bool Activo);

public record TipoContactoDetailDto(long Id, string Nombre, string? Observaciones, bool Activo);

public class TipoContactoUpsertDto
{
    [Required(ErrorMessage = "El nombre es requerido.")]
    [StringLength(60, ErrorMessage = "El nombre no puede superar 60 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(250, ErrorMessage = "Las observaciones no pueden superar 250 caracteres.")]
    public string? Observaciones { get; set; }

    public bool Activo { get; set; } = true;
}
```

**Step 3: `ProveedorUpsertDto`**

Quitar `NombreContacto`, `Telefono` y `Email` (líneas 27-34) y agregar la lista. El compilador señalará los usos que hay que ajustar (Task 8).

```csharp
    public List<ProveedorCuentaBancariaDto> CuentasBancarias { get; set; } = new();

    public List<ProveedorContactoDto> Contactos { get; set; } = new();
```

**Step 4: `ProveedorDetailDto`**

Conserva `NombreContacto` / `Telefono` / `Email` (los alimenta el contacto #1) y suma la lista, después de `CuentasBancarias`:

```csharp
    IReadOnlyList<ProveedorContactoDto> Contactos,
```

---

## Task 4: Normalizador de contactos (TDD, sin base de datos)

Se extrae a una clase propia — como `AbonoCompromisoCalculator` y `ChequeNumeracionCalculator` — porque `ProveedoresService.CreateAsync` abre su propia transacción y no es testeable con el fixture de rollback. Toda la validación queda cubierta por tests unitarios rápidos.

**Files:**
- Create: `SIAD.Services/Proveedores/ProveedorContactosNormalizer.cs`
- Test: `SIAD.Tests/Proveedores/ProveedorContactosNormalizerTests.cs`

**Step 1: Escribir los tests que fallan**

```csharp
using System;
using System.Collections.Generic;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Services.Proveedores;
using Xunit;

namespace SIAD.Tests.Proveedores;

public class ProveedorContactosNormalizerTests
{
    private static ProveedorContactoDto Row(string? nombre = null, string? email = null,
        string? telefono = null, long? tipoId = null) =>
        new() { Nombre = nombre, Email = email, Telefono = telefono, TipoContactoId = tipoId };

    [Fact]
    public void Normalize_DescartaFilasVacias()
    {
        var result = ProveedorContactosNormalizer.Normalize(
            new List<ProveedorContactoDto> { Row(), Row("  "), Row("Ana") });

        Assert.Single(result);
        Assert.Equal("Ana", result[0].Nombre);
    }

    [Fact]
    public void Normalize_SinContactos_DevuelveListaVacia()
    {
        Assert.Empty(ProveedorContactosNormalizer.Normalize(new List<ProveedorContactoDto>()));
        Assert.Empty(ProveedorContactosNormalizer.Normalize(null));
    }

    [Fact]
    public void Normalize_FilaConDatosPeroSinNombre_Lanza()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ProveedorContactosNormalizer.Normalize(
                new List<ProveedorContactoDto> { Row(telefono: "2222-3333") }));

        Assert.Contains("fila 1", ex.Message);
    }

    [Fact]
    public void Normalize_ReasignaOrdenConsecutivo()
    {
        var result = ProveedorContactosNormalizer.Normalize(
            new List<ProveedorContactoDto> { Row("Ana"), Row("  "), Row("Beto") });

        Assert.Equal(1, result[0].Orden);
        Assert.Equal(2, result[1].Orden);
    }

    [Fact]
    public void Normalize_RecortaEspacios()
    {
        var result = ProveedorContactosNormalizer.Normalize(
            new List<ProveedorContactoDto> { Row("  Ana  ", email: "  ana@x.com ") });

        Assert.Equal("Ana", result[0].Nombre);
        Assert.Equal("ana@x.com", result[0].Email);
    }

    [Fact]
    public void Normalize_EmailInvalido_Lanza()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ProveedorContactosNormalizer.Normalize(
                new List<ProveedorContactoDto> { Row("Ana", email: "ana-arroba-x") }));

        Assert.Contains("email", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_NombresRepetidos_Lanza()
    {
        Assert.Throws<ArgumentException>(() =>
            ProveedorContactosNormalizer.Normalize(
                new List<ProveedorContactoDto> { Row("Ana"), Row(" ana ") }));
    }

    [Fact]
    public void Normalize_NombreMuyLargo_Lanza()
    {
        Assert.Throws<ArgumentException>(() =>
            ProveedorContactosNormalizer.Normalize(
                new List<ProveedorContactoDto> { Row(new string('x', 151)) }));
    }

    [Fact]
    public void BuildLegacyFields_TomaElPrimerContacto()
    {
        var contactos = ProveedorContactosNormalizer.Normalize(
            new List<ProveedorContactoDto>
            {
                Row("Ana", "ana@x.com", "2222-3333"),
                Row("Beto", "beto@x.com", "4444-5555")
            });

        var legacy = ProveedorContactosNormalizer.BuildLegacyFields(contactos);

        Assert.Equal("Ana", legacy.NombreContacto);
        Assert.Equal("2222-3333", legacy.Telefono);
        Assert.Equal("ana@x.com", legacy.Email);
    }

    [Fact]
    public void BuildLegacyFields_TelefonoLargo_SeTruncaA20()
    {
        var contactos = ProveedorContactosNormalizer.Normalize(
            new List<ProveedorContactoDto> { Row("Ana", telefono: "2222-3333 ext 101 / 9999") });

        var legacy = ProveedorContactosNormalizer.BuildLegacyFields(contactos);

        Assert.Equal(20, legacy.Telefono!.Length);
        Assert.Equal("2222-3333 ext 101 / ", legacy.Telefono);
    }

    [Fact]
    public void BuildLegacyFields_SinContactos_DevuelveNulos()
    {
        var legacy = ProveedorContactosNormalizer.BuildLegacyFields(new List<ProveedorContactoDto>());

        Assert.Null(legacy.NombreContacto);
        Assert.Null(legacy.Telefono);
        Assert.Null(legacy.Email);
    }
}
```

**Step 2: Correr los tests y verificar que fallan**

Run: `dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~ProveedorContactosNormalizerTests"`
Expected: error de compilación — `ProveedorContactosNormalizer` no existe.

**Step 3: Implementar**

```csharp
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
```

**Step 4: Correr los tests y verificar que pasan**

Run: `dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~ProveedorContactosNormalizerTests"`
Expected: 10 passed. Estos tests no necesitan `SIAD_TEST_DB`.

---

## Task 5: Servicio — carga y sincronización de contactos

**Files:**
- Modify: `SIAD.Services/Proveedores/ProveedoresService.cs`

**Step 1: Constante de tabla**

Junto a la línea 20:

```csharp
    private const string ProveedorContactoTableName = "prv_proveedor_contacto";
    private const string TipoContactoTableName = "prv_tipo_contacto";
```

**Step 2: `LoadContactosAsync`**

Después de `LoadCuentasBancariasAsync` (línea 861). El filtro por empresa lo aplica el query filter global de `ICompanyScopedEntity`; aquí solo se filtra por proveedor.

```csharp
    private async Task<List<ProveedorContactoDto>> LoadContactosAsync(
        string codigoProveedor,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(ProveedorContactoTableName, cancellationToken))
        {
            return new List<ProveedorContactoDto>();
        }

        return await (
            from c in _context.prv_proveedor_contactos.AsNoTracking()
            join t in _context.prv_tipo_contactos.AsNoTracking()
                on c.tipo_contacto_id equals t.tipo_contacto_id into tipos
            from t in tipos.DefaultIfEmpty()
            where c.cod_proveedor == codigoProveedor
            orderby c.orden, c.proveedor_contacto_id
            select new ProveedorContactoDto
            {
                ProveedorContactoId = c.proveedor_contacto_id,
                TipoContactoId = c.tipo_contacto_id,
                TipoContacto = t == null ? null : t.nombre,
                Nombre = c.nombre,
                Cargo = c.cargo,
                Telefono = c.telefono,
                Extension = c.extension,
                Celular = c.celular,
                Email = c.email,
                Observaciones = c.observaciones,
                Orden = c.orden
            }).ToListAsync(cancellationToken);
    }
```

**Step 3: `SyncContactosAsync`**

Después de `SyncCuentasBancariasAsync` (línea 1178). Mismo diff por id: borra ausentes, actualiza existentes, inserta nuevos.

```csharp
    private async Task SyncContactosAsync(
        string codigoProveedor,
        IReadOnlyList<ProveedorContactoDto> contactos,
        string user,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(ProveedorContactoTableName, cancellationToken))
        {
            return;
        }

        await ValidarTiposContactoAsync(contactos, cancellationToken);

        var usuario = NormalizeUser(user);
        var fechaCreacion = await GetCurrentDatabaseTimestampAsync(
            ProveedorContactoTableName, "fecha_creacion", cancellationToken);
        var fechaModificacion = await GetCurrentDatabaseTimestampAsync(
            ProveedorContactoTableName, "fecha_modificacion", cancellationToken);

        var existentes = await _context.prv_proveedor_contactos
            .Where(x => x.cod_proveedor == codigoProveedor)
            .ToListAsync(cancellationToken);

        var existentesPorId = existentes.ToDictionary(x => x.proveedor_contacto_id);
        var idsEnviados = contactos
            .Where(x => x.ProveedorContactoId.HasValue && x.ProveedorContactoId.Value > 0)
            .Select(x => x.ProveedorContactoId!.Value)
            .ToHashSet();

        foreach (var existente in existentes.Where(x => !idsEnviados.Contains(x.proveedor_contacto_id)))
        {
            _context.prv_proveedor_contactos.Remove(existente);
        }

        foreach (var item in contactos)
        {
            var orden = item.Orden > 0 ? item.Orden : 1;

            if (item.ProveedorContactoId is long id && id > 0)
            {
                if (!existentesPorId.TryGetValue(id, out var existente))
                {
                    throw new InvalidOperationException(
                        $"No se encontró el contacto {id} para el proveedor {codigoProveedor}.");
                }

                var hayCambios = existente.tipo_contacto_id != item.TipoContactoId
                    || !string.Equals(existente.nombre, item.Nombre, StringComparison.Ordinal)
                    || !string.Equals(existente.cargo, item.Cargo, StringComparison.Ordinal)
                    || !string.Equals(existente.telefono, item.Telefono, StringComparison.Ordinal)
                    || !string.Equals(existente.extension, item.Extension, StringComparison.Ordinal)
                    || !string.Equals(existente.celular, item.Celular, StringComparison.Ordinal)
                    || !string.Equals(existente.email, item.Email, StringComparison.Ordinal)
                    || !string.Equals(existente.observaciones, item.Observaciones, StringComparison.Ordinal)
                    || existente.orden != orden;

                existente.tipo_contacto_id = item.TipoContactoId;
                existente.nombre = item.Nombre!;
                existente.cargo = item.Cargo;
                existente.telefono = item.Telefono;
                existente.extension = item.Extension;
                existente.celular = item.Celular;
                existente.email = item.Email;
                existente.observaciones = item.Observaciones;
                existente.orden = orden;

                if (hayCambios)
                {
                    existente.fecha_modificacion = fechaModificacion;
                    existente.usuario_modifica = usuario;
                }

                continue;
            }

            _context.prv_proveedor_contactos.Add(new prv_proveedor_contacto
            {
                cod_proveedor = codigoProveedor,
                tipo_contacto_id = item.TipoContactoId,
                nombre = item.Nombre!,
                cargo = item.Cargo,
                telefono = item.Telefono,
                extension = item.Extension,
                celular = item.Celular,
                email = item.Email,
                observaciones = item.Observaciones,
                orden = orden,
                fecha_creacion = fechaCreacion,
                usuario_creo = usuario
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    // Un tipo de otra empresa nunca llega hasta acá (query filter global), pero un id
    // inventado o de un tipo inactivo sí: se rechaza con mensaje claro en vez de dejar
    // que reviente la FK.
    private async Task ValidarTiposContactoAsync(
        IReadOnlyList<ProveedorContactoDto> contactos,
        CancellationToken cancellationToken)
    {
        var ids = contactos
            .Where(x => x.TipoContactoId.HasValue)
            .Select(x => x.TipoContactoId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return;
        }

        var validos = await _context.prv_tipo_contactos
            .AsNoTracking()
            .Where(t => ids.Contains(t.tipo_contacto_id) && t.activo)
            .Select(t => t.tipo_contacto_id)
            .ToListAsync(cancellationToken);

        var invalido = ids.FirstOrDefault(id => !validos.Contains(id));
        if (invalido != 0)
        {
            throw new ArgumentException(
                $"El tipo de contacto {invalido} no existe o está inactivo.", nameof(contactos));
        }
    }
```

**Step 4: Enganchar en `CreateAsync`**

En `CreateAsync`, junto a la línea 457:

```csharp
        var cuentasBancarias = PrepareCuentasBancarias(dto);
        var contactos = ProveedorContactosNormalizer.Normalize(dto.Contactos);
        var legacyContacto = ProveedorContactosNormalizer.BuildLegacyFields(contactos);
```

En los dos `INSERT` (líneas 510-512 y 553-555), reemplazar los tres valores por los derivados:

```csharp
                    OptionalText(legacyContacto.NombreContacto),
                    OptionalText(legacyContacto.Telefono),
                    OptionalText(legacyContacto.Email),
```

En el bloque de bitácora (línea 564-565), usar los mismos valores:

```csharp
            new("telefono", null, legacyContacto.Telefono),
            new("email", null, legacyContacto.Email),
```

Y antes del commit (después de la línea 571):

```csharp
        await SyncContactosAsync(codigo, contactos, user, cancellationToken);
```

**Step 5: Enganchar en `UpdateAsync`**

Mismo patrón: normalizar junto a la línea 589, sustituir los tres `OptionalText(NormalizeOptional(dto.X))` de los dos `UPDATE` (líneas 640-642 y 676-678), ajustar los `Diff("telefono", ...)` y `Diff("email", ...)` (líneas 702-703) y agregar después de la línea 710:

```csharp
        await SyncContactosAsync(codigoNormalizado, contactos, user, cancellationToken);
```

**Step 6: `GetProveedorAsync` devuelve los contactos**

Junto a la línea 226 se carga `cuentasBancarias`; agregar:

```csharp
        var contactos = await LoadContactosAsync(codigoNormalizado, cancellationToken);
```

y pasarlo al `ProveedorDetailDto` en la posición correspondiente.

**Step 7: `DeleteAsync` limpia los contactos**

En `DeleteAsync` ya hay un bloque que borra las cuentas bancarias si la tabla existe (línea 720). Agregar el equivalente para contactos, antes de borrar el proveedor:

```csharp
        if (await TableExistsAsync(ProveedorContactoTableName, cancellationToken))
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"DELETE FROM public.prv_proveedor_contacto
                   WHERE cod_proveedor = {codigoNormalizado}
                     AND company_id = {EnsureCompanyId()}",
                cancellationToken);
        }
```

**Step 8: Compilar**

Run: `dotnet build HODSOFT_DEVEXPRESS.sln`
Expected: fallan las referencias a `dto.NombreContacto` / `dto.Telefono` / `dto.Email` que queden — se resuelven aquí y en Task 8.

---

## Task 6: Servicio — catálogo de tipos de contacto

**Files:**
- Modify: `SIAD.Services/Proveedores/IProveedoresService.cs`
- Modify: `SIAD.Services/Proveedores/ProveedoresService.cs`

**Step 1: Interfaz** — agregar al final:

```csharp
    Task<IReadOnlyList<TipoContactoLookupDto>> GetTiposContactoAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TipoContactoListItemDto>> GetTiposContactoCatalogoAsync(CancellationToken cancellationToken = default);

    Task<TipoContactoDetailDto?> GetTipoContactoAsync(long id, CancellationToken cancellationToken = default);

    Task<long> CreateTipoContactoAsync(TipoContactoUpsertDto dto, string user, CancellationToken cancellationToken = default);

    Task UpdateTipoContactoAsync(long id, TipoContactoUpsertDto dto, string user, CancellationToken cancellationToken = default);

    Task DeleteTipoContactoAsync(long id, CancellationToken cancellationToken = default);
```

**Step 2: Implementación**, después de `DeleteTipoAsync` (línea 836). Espejo del CRUD de tipos de proveedor, más `activo`, auditoría de usuario y unicidad de nombre por empresa:

```csharp
    public async Task<IReadOnlyList<TipoContactoLookupDto>> GetTiposContactoAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(TipoContactoTableName, cancellationToken))
        {
            return Array.Empty<TipoContactoLookupDto>();
        }

        return await _context.prv_tipo_contactos
            .AsNoTracking()
            .Where(t => t.activo)
            .OrderBy(t => t.nombre)
            .Select(t => new TipoContactoLookupDto(t.tipo_contacto_id, t.nombre))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TipoContactoListItemDto>> GetTiposContactoCatalogoAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(TipoContactoTableName, cancellationToken))
        {
            return Array.Empty<TipoContactoListItemDto>();
        }

        return await _context.prv_tipo_contactos
            .AsNoTracking()
            .OrderBy(t => t.nombre)
            .Select(t => new TipoContactoListItemDto(t.tipo_contacto_id, t.nombre, t.observaciones, t.activo))
            .ToListAsync(cancellationToken);
    }

    public async Task<TipoContactoDetailDto?> GetTipoContactoAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await _context.prv_tipo_contactos
            .AsNoTracking()
            .Where(t => t.tipo_contacto_id == id)
            .Select(t => new TipoContactoDetailDto(t.tipo_contacto_id, t.nombre, t.observaciones, t.activo))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<long> CreateTipoContactoAsync(
        TipoContactoUpsertDto dto,
        string user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var nombre = NormalizeRequired(dto.Nombre, 60, "nombre");
        await EnsureNombreTipoContactoDisponibleAsync(nombre, null, cancellationToken);

        var entity = new prv_tipo_contacto
        {
            nombre = nombre,
            observaciones = NormalizeOptional(dto.Observaciones, 250, "observaciones"),
            activo = dto.Activo,
            fecha_creacion = await GetCurrentDatabaseTimestampAsync(
                TipoContactoTableName, "fecha_creacion", cancellationToken),
            usuario_creo = NormalizeUser(user)
        };

        _context.prv_tipo_contactos.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.tipo_contacto_id;
    }

    public async Task UpdateTipoContactoAsync(
        long id,
        TipoContactoUpsertDto dto,
        string user,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El identificador del tipo de contacto no es válido.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(dto);

        var entity = await _context.prv_tipo_contactos
            .FirstOrDefaultAsync(t => t.tipo_contacto_id == id, cancellationToken);

        if (entity is null)
        {
            throw new KeyNotFoundException($"No se encontró el tipo de contacto {id}.");
        }

        var nombre = NormalizeRequired(dto.Nombre, 60, "nombre");
        await EnsureNombreTipoContactoDisponibleAsync(nombre, id, cancellationToken);

        entity.nombre = nombre;
        entity.observaciones = NormalizeOptional(dto.Observaciones, 250, "observaciones");
        entity.activo = dto.Activo;
        entity.fecha_modificacion = await GetCurrentDatabaseTimestampAsync(
            TipoContactoTableName, "fecha_modificacion", cancellationToken);
        entity.usuario_modifica = NormalizeUser(user);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTipoContactoAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El identificador del tipo de contacto no es válido.", nameof(id));
        }

        var enUso = await _context.prv_proveedor_contactos
            .AsNoTracking()
            .AnyAsync(c => c.tipo_contacto_id == id, cancellationToken);

        if (enUso)
        {
            throw new InvalidOperationException(
                "No se puede eliminar el tipo porque está asignado a uno o más contactos. Puede desactivarlo.");
        }

        var entity = await _context.prv_tipo_contactos
            .FirstOrDefaultAsync(t => t.tipo_contacto_id == id, cancellationToken);

        if (entity is null)
        {
            throw new KeyNotFoundException($"No se encontró el tipo de contacto {id}.");
        }

        _context.prv_tipo_contactos.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNombreTipoContactoDisponibleAsync(
        string nombre,
        long? idActual,
        CancellationToken cancellationToken)
    {
        var repetido = await _context.prv_tipo_contactos
            .AsNoTracking()
            .AnyAsync(
                t => t.nombre.ToUpper() == nombre.ToUpper()
                    && (idActual == null || t.tipo_contacto_id != idActual),
                cancellationToken);

        if (repetido)
        {
            throw new InvalidOperationException($"Ya existe un tipo de contacto llamado \"{nombre}\".");
        }
    }
```

**Step 3: Compilar**

Run: `dotnet build HODSOFT_DEVEXPRESS.sln`

---

## Task 7: Endpoints del controlador

**Files:**
- Modify: `apc/Controllers/ProveedoresController.cs`

Los contactos del proveedor NO llevan endpoint propio: viajan dentro del upsert. Solo se expone el catálogo. La clase ya tiene `[ModuleAuthorize(PermissionModules.Proveedores)]`, así que los nuevos endpoints quedan cubiertos sin registrar nada en `PermissionNames`.

**Step 1: Agregar los endpoints** siguiendo el estilo de los de `tipos` (líneas 224-300) — mismo manejo de excepciones (`KeyNotFoundException` → `NotFound`, `InvalidOperationException`/`ArgumentException` → `BadRequest` con `Problem(detail:)`), y pasando `User.Identity?.Name` como usuario, igual que hacen los endpoints de bancos.

```csharp
    [HttpGet("contactos/tipos")]
    public async Task<IActionResult> GetTiposContacto(CancellationToken cancellationToken)
        => Ok(await _proveedoresService.GetTiposContactoAsync(cancellationToken));

    [HttpGet("contactos/tipos/catalogo")]
    public async Task<IActionResult> GetTiposContactoCatalogo(CancellationToken cancellationToken)
        => Ok(await _proveedoresService.GetTiposContactoCatalogoAsync(cancellationToken));

    [HttpGet("contactos/tipos/{id:long}")]
    public async Task<IActionResult> GetTipoContacto(long id, CancellationToken cancellationToken)
    {
        var tipo = await _proveedoresService.GetTipoContactoAsync(id, cancellationToken);
        return tipo is null ? NotFound() : Ok(tipo);
    }
```

Más `POST contactos/tipos`, `PUT contactos/tipos/{id:long}` y `DELETE contactos/tipos/{id:long}`.

> Ojo con el orden de rutas: `contactos/tipos/{id:long}` no colisiona con `tipos/{id:int}` porque el prefijo es distinto.

**Step 2: Compilar y verificar el ruteo**

Run: `dotnet build HODSOFT_DEVEXPRESS.sln`

---

## Task 8: Cliente HTTP y ajuste del mapeo en la página de edición

**Files:**
- Modify: `apc.Client/Services/Proveedores/ProveedoresClient.cs`
- Modify: `apc.Client/Pages/Proveedores/ProveedorEdit.razor`

**Step 1: Métodos del cliente**, junto a los de tipos de proveedor (líneas 116-166):

```csharp
    public async Task<TipoContactoLookupDto[]> ObtenerTiposContactoAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsyncWithAuthCheck<TipoContactoLookupDto[]>("api/proveedores/contactos/tipos", ct)
           ?? Array.Empty<TipoContactoLookupDto>();

    public async Task<TipoContactoListItemDto[]> ObtenerTiposContactoCatalogoAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsyncWithAuthCheck<TipoContactoListItemDto[]>("api/proveedores/contactos/tipos/catalogo", ct)
           ?? Array.Empty<TipoContactoListItemDto>();

    public async Task<TipoContactoDetailDto?> ObtenerTipoContactoAsync(long id, CancellationToken ct = default)
        => id <= 0
            ? null
            : await http.GetFromJsonAsyncWithAuthCheck<TipoContactoDetailDto?>($"api/proveedores/contactos/tipos/{id}", ct);

    public async Task CrearTipoContactoAsync(TipoContactoUpsertDto dto, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsyncWithAuthCheck("api/proveedores/contactos/tipos", dto, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task ActualizarTipoContactoAsync(long id, TipoContactoUpsertDto dto, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsyncWithAuthCheck($"api/proveedores/contactos/tipos/{id}", dto, ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }

    public async Task EliminarTipoContactoAsync(long id, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"api/proveedores/contactos/tipos/{id}", ct);
        await EnsureSuccessWithDetailsAsync(response, ct);
    }
```

**Step 2: `ProveedorEdit.razor`**

- Agregar `private readonly List<TipoContactoLookupDto> tiposContacto = new();` y cargarlo dentro del `Task.WhenAll` de `CargarDatosAsync` (línea 105).
- Quitar del mapeo de `CargarProveedorAsync` las líneas 188-190 (`NombreContacto`, `Telefono`, `Email`) y agregar la lista de contactos:

```csharp
            Contactos = detail.Contactos
                .OrderBy(x => x.Orden)
                .Select((x, index) => new ProveedorContactoDto
                {
                    ProveedorContactoId = x.ProveedorContactoId,
                    TipoContactoId = x.TipoContactoId,
                    Nombre = x.Nombre,
                    Cargo = x.Cargo,
                    Telefono = x.Telefono,
                    Extension = x.Extension,
                    Celular = x.Celular,
                    Email = x.Email,
                    Observaciones = x.Observaciones,
                    Orden = index + 1
                })
                .ToList(),
```

A diferencia de las cuentas bancarias, si la lista viene vacía se deja vacía: no se agrega fila en blanco.

- En el alta (línea 115), `Contactos` queda como lista vacía.
- Agregar `NormalizeContactos()` junto a `NormalizeCuentaBancarias()` y llamarlo en `GuardarAsync`: descarta filas totalmente vacías y renumera `Orden`. Es el mismo criterio del normalizador del servidor, aplicado antes de enviar para que el usuario vea el error sin viaje al servidor.
- Pasar `TiposContacto="tiposContacto"` al componente `ProveedorForm`.

---

## Task 9: Grid de contactos en el formulario

**REQUIRED SUB-SKILL:** `hodsoft-blazor-devexpress-ui`. Antes de tocar cualquier API de DevExpress, consultar el MCP `dxdocs` (`devexpress_docs_search` → `devexpress_docs_get_content`).

**Files:**
- Modify: `apc.Client/Pages/Proveedores/ProveedorForm.razor`

**Step 1: Quitar el grupo "Contacto"** (líneas 54-64 completas).

**Step 2: Agregar el grupo "Contactos"** después de "Datos generales", copiando la estructura del grupo "Cuentas bancarias" (líneas 87-166). Columnas y anchos:

| Columna | Editor | Ancho |
|---|---|---|
| Tipo | `DxComboBox` sobre `TiposContacto`, `DropDownStyle.DropDownList`, `ClearButtonDisplayMode.Auto` | 160px |
| Nombre | `DxTextBox` MaxLength 150 | — |
| Cargo | `DxTextBox` MaxLength 100 | 160px |
| Teléfono | `DxTextBox` MaxLength 30 | 130px |
| Ext. | `DxTextBox` MaxLength 10 | 80px |
| Celular | `DxTextBox` MaxLength 30 | 130px |
| Email | `DxTextBox` MaxLength 150 | — |
| Observaciones | `DxTextBox` MaxLength 500 | 200px |
| (quitar) | botón `btn-icon btn-delete` | 60px |

El combo de tipo enlaza `TValue="long?"` contra `item.TipoContactoId`, con `ValueFieldName="@nameof(TipoContactoLookupDto.Id)"` y `TextFieldName="@nameof(TipoContactoLookupDto.Nombre)"`.

Igual que en cuentas bancarias, el grid usa `@key="@Model.Contactos.Count"` para re-renderizar al agregar o quitar filas.

**Step 3: Parámetro y handlers**

```csharp
    [Parameter]
    public IReadOnlyList<TipoContactoLookupDto> TiposContacto { get; set; } = Array.Empty<TipoContactoLookupDto>();

    private void AddContacto()
    {
        Model.Contactos.Add(new ProveedorContactoDto { Orden = Model.Contactos.Count + 1 });
    }

    private void RemoveContacto(int index)
    {
        if (index < 0 || index >= Model.Contactos.Count)
        {
            return;
        }

        Model.Contactos.RemoveAt(index);

        for (var i = 0; i < Model.Contactos.Count; i++)
        {
            Model.Contactos[i].Orden = i + 1;
        }
    }
```

**Step 4:** En `OnParametersSet` (línea 217) **no** agregar contacto vacío: son opcionales. Si la lista está vacía, el grid muestra su mensaje de "sin datos" y el usuario usa "Agregar contacto".

**Step 5: Verificar en el navegador**

Levantar el preview del portal, entrar a `/proveedores/nuevo` y a la edición de un proveedor existente. Comprobar: agregar y quitar filas, que el combo de tipo cargue, y que guardar sin contactos funcione. Revisar la consola del navegador y los logs del servidor.

---

## Task 10: Contactos en el detalle del proveedor

**Files:**
- Modify: `apc.Client/Pages/Proveedores/Components/ProveedorDetailGeneral.razor`

**Step 1:** Agregar un grupo "Contactos" entre "Datos generales" y "Cuentas bancarias", con la misma tabla HTML de solo lectura que usa el grupo de cuentas bancarias (líneas 27-58): columnas Tipo, Nombre, Cargo, Teléfono, Ext., Celular, Email, Observaciones; y el mensaje `No hay contactos registrados.` cuando la lista viene vacía.

**Step 2:** Dejar "Nombre contacto", "Telefono" y "Email" en "Datos generales" — siguen alimentados por el contacto #1 y sirven de contraste al verificar la sincronía.

---

## Task 11: Mantenimiento del catálogo de tipos de contacto

**REQUIRED SUB-SKILL:** `hodsoft-blazor-devexpress-ui` (estándar de grids).

**Files:**
- Create: `apc.Client/Pages/Proveedores/TiposContactoList.razor`
- Create: `apc.Client/Pages/Proveedores/TipoContactoEdit.razor`
- Create: `apc.Client/Pages/Proveedores/TipoContactoForm.razor`
- Modify: `apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs:143`

**Step 1:** Clonar `TiposProveedorList.razor` / `TipoProveedorEdit.razor` / `TipoProveedorForm.razor` cambiando:

- Rutas: `/mantenimientos/tipos-contacto`, `/mantenimientos/tipos-contacto/nuevo`, `/mantenimientos/tipos-contacto/{id:long}/editar`.
- Título "Tipos de contacto", subtítulo "Gestión de tipos de contacto de proveedor".
- Id `long` en vez de `int`.
- Columna extra "Activo" en el grid (`Si` / `No`) y un toggle en el formulario, con el mismo `label.toggle-switch` que usa `ProveedorForm` para "Activo".
- El botón "Volver a proveedores" apunta igual a `/proveedores`.

**Step 2:** Entrada en el sidebar, junto a la de tipos de proveedor:

```csharp
                        new SidebarNavItem { Id = "mant-tipos-contacto", Text = "Tipos de contacto", NavigateUrl = "/mantenimientos/tipos-contacto", MatchPrefixes = ["/mantenimientos/tipos-contacto"], IconCssClass = "bi bi-person-lines-fill" }
```

**Step 3:** Si la página lleva su propio `DxToastProvider`, debe declarar `StickToViewport="true"` (convención del repo).

**Step 4: Verificar en el navegador** — alta, edición, desactivación y borrado; que borrar un tipo en uso muestre el mensaje del servicio.

---

## Task 12: Auditoría

**Files:**
- Modify: `SIAD.Core/Constants/AuditableMaestros.cs`
- Create: `Database/2026-07-27_bitacora_config_contactos.sql`

**Step 1:** Agregar las dos tablas a la lista blanca (después de la línea 32):

```csharp
        new("prv_proveedor_contacto",         "Contactos de proveedor",         "Proveedores"),
        new("prv_tipo_contacto",              "Tipos de contacto",              "Proveedores"),
```

Ambas son entidades con clave y se persisten con `SaveChanges`, así que las ve el interceptor: no hay que llamar a `IBitacoraMaestrosWriter` a mano (a diferencia de `prv_proveedores`, que va por SQL crudo).

**Step 2:** Script con los `INSERT` en `bitacora_maestro_config` / `bitacora_maestro_catalogo`, siguiendo el formato de los scripts de configuración de bitácora ya existentes en `Database/`. Registrarlo también en el runbook.

**Step 3:** Correr los tests de auditoría existentes para confirmar que la lista blanca sigue coherente:

Run: `dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~Auditoria"`

---

## Task 13: Tests de integración

Requieren `SIAD_TEST_DB` apuntando a una base **de prueba** con el script del Task 1 ya aplicado. Sin la variable quedan `Skipped`.

**Files:**
- Create: `SIAD.Tests/Proveedores/ProveedorContactosTests.cs`

**Step 1: Escribir los tests**

Seguir el patrón de `SIAD.Tests/Auditoria/ProveedorAuditTests.cs`: `[Collection("Postgres")]`, `IntegrationTestBase`, `TestCurrentCompanyService`, `_context.Database.UseTransaction(Transaction)` y `[SkippableFact]` con `Skip.IfNot(Fixture.Available, ...)`.

Casos:

1. `GetTiposContactoCatalogo_DevuelveSoloLosDeLaEmpresa` — insertar un tipo en la empresa del fixture y otro en una empresa distinta (con `IgnoreQueryFilters` para sembrarlo); el catálogo devuelve solo el primero.
2. `CreateTipoContacto_NombreRepetido_Falla` — `InvalidOperationException`.
3. `DeleteTipoContacto_EnUso_Falla` — sembrar un contacto que lo referencie; esperar `InvalidOperationException`.
4. `DeleteTipoContacto_SinUso_Borra`.
5. `GetProveedorAsync_NoDevuelveContactosDeOtraEmpresa` — sembrar dos proveedores con el **mismo** `cod_proveedor` en empresas distintas, cada uno con su contacto; el detalle de la empresa del fixture trae solo el suyo. Este es el test que justifica `company_id` en la tabla hija.

`CreateAsync` / `UpdateAsync` completos **no** se pueden probar aquí: abren su propia transacción y Npgsql no admite transacciones anidadas con el fixture de rollback (mismo motivo documentado en `ProveedorAuditTests.cs:80-85`). Esa lógica queda cubierta por los tests unitarios del Task 4 más la verificación manual del Task 9. Dejar un `[SkippableFact]` con `Skip.If(true, "...")` y el comentario explicando por qué, siguiendo la convención del repo.

**Step 2: Correr los tests**

Run: `$env:SIAD_TEST_DB = '<conn de prueba>'; dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~ProveedorContactos"`
Expected: pasan (o `Skipped` si no hay `SIAD_TEST_DB`).

---

## Task 14: Verificación final

**REQUIRED SUB-SKILL:** `verification-before-completion`.

**Step 1:** `dotnet build HODSOFT_DEVEXPRESS.sln` — sin errores ni warnings nuevos.

**Step 2:** `dotnet test SIAD.Tests/SIAD.Tests.csproj` — suite completa, sin regresiones.

**Step 3:** Recorrido en el navegador: alta de proveedor con dos contactos → guardar → reabrir y confirmar que persisten en orden; quitar el primero → confirmar que `nombre_contacto` del proveedor pasa a ser el que quedó primero; alta sin contactos → guarda sin error; mantenimiento del catálogo completo.

**Step 4:** Resumir qué quedó pendiente de aplicar en base de datos (los dos scripts de `Database/`) y el hallazgo anotado sobre `prv_proveedor_cuenta_bancaria` sin `company_id`.
