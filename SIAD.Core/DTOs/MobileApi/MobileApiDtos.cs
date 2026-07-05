using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.MobileApi;

// =============================================================================
// DTOs de la API móvil de lectores (apc.MobileApi, plan app_lectores L3).
// Contrato REST/JSON limpio (sin el envelope WCF del WS viejo) con paridad
// FUNCIONAL: los mismos SPs V3 producen los mismos datos. La app Flutter (L4+)
// consume esto; el WS WCF viejo queda intacto para la app Java.
// =============================================================================

// ----------------------------------------------------------------------------
// Autenticación / sesión
// ----------------------------------------------------------------------------

/// <summary>Login del lector: código + clave (+ dispositivo informativo).</summary>
public sealed class LoginRequest
{
    public string Codigo { get; init; } = string.Empty;
    public string Clave { get; init; } = string.Empty;
    public string? Dispositivo { get; init; }
}

/// <summary>Perfil del lector devuelto en el login y en /perfil.</summary>
public sealed class LectorPerfilDto
{
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Ruta { get; init; } = string.Empty;
    public int? CodCiclo { get; init; }
}

/// <summary>Respuesta del login: token bearer + expiración + perfil.</summary>
public sealed class LoginRespuesta
{
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiraAt { get; init; }
    public LectorPerfilDto Lector { get; init; } = new();
}

/// <summary>
/// Contexto de la sesión resuelto desde el token (uso interno del middleware).
/// Es la credencial que resuelve el tenant (A6): company_id NUNCA viene del
/// cliente.
/// </summary>
public sealed class LectorSesionContexto
{
    public long CompanyId { get; init; }
    public long CredencialId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Ruta { get; init; } = string.Empty;
    public int? CodCiclo { get; init; }
}

// ----------------------------------------------------------------------------
// Ciclo / ruta
// ----------------------------------------------------------------------------

/// <summary>Ciclo pendiente de una ruta (paridad GetCiclo).</summary>
public sealed class InformacionCicloDto
{
    public bool Encontrado { get; init; }
    public int Ciclo { get; init; }
    public int Anio { get; init; }
    public int Mes { get; init; }
}

/// <summary>
/// Medidor de una ruta (paridad MedidorEntidad del WS). Los campos de lectura
/// van como string y los saldos/recargos como decimal, igual que el WS, para
/// preservar la paridad de valores con la app Java.
/// </summary>
public sealed class MedidorDto
{
    public string Clave { get; set; } = string.Empty;
    public string Medidor { get; set; } = string.Empty;
    public string Identidad { get; set; } = string.Empty;
    public string NombreInquilino { get; set; } = string.Empty;
    public string Descuento { get; set; } = string.Empty;
    public string TieneDescuento { get; set; } = "N";
    public string Ciclo { get; set; } = string.Empty;
    public string Ruta { get; set; } = string.Empty;
    public string Secuencia { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Tipo { get; set; } = "1";
    public string TieneMedidor { get; set; } = "N";
    public string LecturaAnterior { get; set; } = string.Empty;
    public string FechaLecturaAnterior { get; set; } = string.Empty;
    public string LecturaActual { get; set; } = string.Empty;
    public string FechaLecturaActual { get; set; } = string.Empty;
    public string DireccionCliente { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Rtn { get; set; } = string.Empty;

    public string Ser1 { get; set; } = "N";
    public string Ser2 { get; set; } = "N";
    public string Ser3 { get; set; } = "N";
    public string Ser4 { get; set; } = "N";
    public string Ser5 { get; set; } = "N";
    public string Ser6 { get; set; } = "N";
    public string Ser7 { get; set; } = "N";
    public string Ser8 { get; set; } = "N";
    public string Ser9 { get; set; } = "N";
    public string Ser10 { get; set; } = "N";

    public decimal SdoAgua { get; set; }
    public decimal SdoAlcantarillado { get; set; }
    public decimal SdoAmbiental { get; set; }
    public decimal SdoConvenio { get; set; }
    public decimal SdoOtros { get; set; }
    public decimal SdoErsap { get; set; }
    public decimal SdoGestionLegal { get; set; }
    public decimal SdoSer6 { get; set; }
    public decimal SdoSer7 { get; set; }
    public decimal SdoSer8 { get; set; }
    public decimal SdoSer9 { get; set; }
    public decimal SdoSer10 { get; set; }

    public decimal RecargoSdoAgua { get; set; }
    public decimal RecargoSdoAmbiental { get; set; }
    public decimal RecargoSdoConvenio { get; set; }
    public decimal RecargoSdoOtros { get; set; }
    public decimal RecargoSdoErsap { get; set; }
    public decimal RecargoSdoGestionLegal { get; set; }
    public decimal RecargoSdoSer6 { get; set; }
    public decimal RecargoSdoSer7 { get; set; }
    public decimal RecargoSdoSer8 { get; set; }
    public decimal RecargoSdoSer9 { get; set; }
    public decimal RecargoSdoSer10 { get; set; }

    public decimal Promedio6Meses { get; set; }
}

// ----------------------------------------------------------------------------
// Snapshot offline V3
// ----------------------------------------------------------------------------

/// <summary>Snapshot offline de una ruta (paridad OfflineSnapshotRutaV3Respuesta).</summary>
public sealed class OfflineSnapshotRutaDto
{
    public bool Success { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = SnapshotContractVersion;
    public string Ruta { get; set; } = string.Empty;
    public int Ciclo { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string GeneratedAt { get; set; } = string.Empty;
    public List<OfflineSnapshotClienteDto> Items { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public const string SnapshotContractVersion = "OFFLINE_SNAPSHOT_V3_2";
}

/// <summary>Snapshot de un cliente (paridad OfflineSnapshotClienteV3Item).</summary>
public sealed class OfflineSnapshotClienteDto
{
    public bool Success { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public long CompanyId { get; set; }
    public long ClienteId { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Medidor { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = OfflineSnapshotRutaDto.SnapshotContractVersion;
    /// <summary>Snapshot JSON del SP con el bloque cai_offline inyectado.</summary>
    public string PackageJson { get; set; } = string.Empty;
}

// ----------------------------------------------------------------------------
// Actualizar lectura V3
// ----------------------------------------------------------------------------

/// <summary>Payload de subida de lectura (paridad MedidorModeloV3).</summary>
public sealed class LecturaV3Request
{
    public int Anio { get; init; }
    public int Mes { get; init; }
    public string? Contador { get; init; }
    public DateTime? FechaLecturaActual { get; init; }
    public string? Usuario { get; init; }
    public decimal LecturaActual { get; init; }
    public string? Ser3 { get; init; }
    public string? Ser4 { get; init; }
    public string? Observacion { get; init; }
    public string? CondicionLectura { get; init; }
    public decimal LecturaPromedio { get; init; }
    public string? NumeroFactura { get; init; }
    public int CorrelativoCai { get; init; }
    public int IdCai { get; init; }
    public string? TieneMedidor { get; init; }
    public string Clave { get; init; } = string.Empty;
    /// <summary>Clave de idempotencia (la maneja sp_lectura_v3).</summary>
    public string? LecturaUuid { get; init; }
    /// <summary>Imagen del medidor en base64 (opcional).</summary>
    public string? Imagen { get; init; }
    public string? Informativo { get; init; }
    public string? Categoria { get; init; }
    /// <summary>
    /// Total calculado por la app; si viene, se valida contra el servidor (preflight).
    /// Nulo = el dispositivo no lo envía y el servidor calcula el suyo (autoritativo).
    /// </summary>
    public decimal? Total { get; init; }
}

/// <summary>Respuesta de la subida de lectura (paridad LecturaRespuestaV3).</summary>
public sealed class LecturaV3Respuesta
{
    public bool Success { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public long FacturaId { get; set; }
    public long NumRecibo { get; set; }
    public string NumeroFactura { get; set; } = string.Empty;
    public long ClienteId { get; set; }
    public string ClienteClave { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public decimal LecturaActual { get; set; }
    public decimal Consumo { get; set; }
    public decimal Subtotal { get; set; }
    public decimal SubtotalAjustes { get; set; }
    public decimal SaldosAnteriores { get; set; }
    public decimal Recargos { get; set; }
    public decimal Total { get; set; }
    public decimal Taservi1 { get; set; }
    public decimal Taservi2 { get; set; }
    public decimal Taservi3 { get; set; }
    public decimal Taservi4 { get; set; }
    public List<LecturaServicioDetalleDto> DetalleServicios { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>Detalle de servicio de la respuesta (paridad LecturaServicioDetalleRespuesta).</summary>
public sealed class LecturaServicioDetalleDto
{
    public string ServicioCodigo { get; set; } = string.Empty;
    public string ServicioNombre { get; set; } = string.Empty;
    public string TipoServicio { get; set; } = string.Empty;
    public string OrigenCalculo { get; set; } = string.Empty;
    public long? ClienteServicioId { get; set; }
    public long? CuadroTarifarioId { get; set; }
    public string CuadroCodigo { get; set; } = string.Empty;
    public string CuadroNombre { get; set; } = string.Empty;
    public long? ReglaTarifariaId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal Monto { get; set; }
    public bool AplicaDescuento { get; set; }
    public decimal MontoDescuento { get; set; }
    public decimal MontoFinal { get; set; }
}

// ----------------------------------------------------------------------------
// Diagnóstico
// ----------------------------------------------------------------------------

/// <summary>Diagnóstico de la API móvil (paridad funcional con Diagnostico del WS).</summary>
public sealed class DiagnosticoDto
{
    public string ServerTime { get; set; } = string.Empty;
    public bool PostgresOk { get; set; }
    public string PostgresServer { get; set; } = string.Empty;
    public string PostgresDatabase { get; set; } = string.Empty;
    public string PostgresError { get; set; } = string.Empty;
    public string Ambiente { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}
