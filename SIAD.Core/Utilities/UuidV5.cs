using System;
using System.Security.Cryptography;
using System.Text;

namespace SIAD.Core.Utilities;

/// <summary>
/// UUID versión 5 (RFC 4122 §4.3): identificador DETERMINISTA derivado de un namespace
/// y un nombre, vía SHA-1. Mismo (namespace, nombre) ⇒ mismo Guid, siempre y en cualquier
/// máquina.
/// <para>
/// Se usa como clave de idempotencia del motor de inventario: el uuid de un asiento se
/// deriva de la identidad del movimiento, de modo que reintentar el mismo posteo produce
/// el mismo uuid y el índice único <c>uq_alm_kardex_company_uuid</c> lo absorbe en vez de
/// duplicar el asiento. .NET no trae UUIDv5 de fábrica.
/// </para>
/// <para>
/// SHA-1 se usa aquí como función de derivación estructural exigida por el RFC, NO como
/// primitiva de seguridad: no protege secretos ni valida integridad frente a un atacante.
/// </para>
/// </summary>
public static class UuidV5
{
    /// <summary>
    /// Namespace propio del módulo de inventario. Es un UUIDv4 fijo y arbitrario: su único
    /// requisito es no cambiar NUNCA, porque de él dependen todos los uuid ya posteados.
    /// </summary>
    public static readonly Guid NamespaceInventario = new("6f9619ff-8b86-d011-b42d-00c04fc964ff");

    /// <summary>
    /// Deriva el UUIDv5 de <paramref name="nombre"/> dentro de <paramref name="ns"/>.
    /// El nombre se codifica en UTF-8 sin normalizar: quien lo construye es responsable de
    /// que sea estable (mismo orden de campos, mismo separador, misma capitalización).
    /// </summary>
    public static Guid Create(Guid ns, string nombre)
    {
        ArgumentNullException.ThrowIfNull(nombre);

        var nsBytes = ToBigEndianBytes(ns);
        var nombreBytes = Encoding.UTF8.GetBytes(nombre);

        var buffer = new byte[nsBytes.Length + nombreBytes.Length];
        Buffer.BlockCopy(nsBytes, 0, buffer, 0, nsBytes.Length);
        Buffer.BlockCopy(nombreBytes, 0, buffer, nsBytes.Length, nombreBytes.Length);

        // SHA-1 por mandato del RFC 4122 para la versión 5 (no es uso criptográfico).
        var hash = SHA1.HashData(buffer);

        var guidBytes = new byte[16];
        Buffer.BlockCopy(hash, 0, guidBytes, 0, 16);

        // Versión 5: nibble alto del octeto 6.
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        // Variante RFC 4122 (10xxxxxx) en el octeto 8.
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return FromBigEndianBytes(guidBytes);
    }

    /// <summary>Atajo para el namespace de inventario.</summary>
    public static Guid CreateInventario(string nombre) => Create(NamespaceInventario, nombre);

    // System.Guid guarda los tres primeros campos en el endianness de la máquina; el RFC
    // los define en big-endian (orden de red). Estas dos conversiones garantizan que el
    // resultado sea idéntico en cualquier arquitectura.
    private static byte[] ToBigEndianBytes(Guid value)
    {
        var bytes = value.ToByteArray();
        InvertirCamposDeGuid(bytes);
        return bytes;
    }

    private static Guid FromBigEndianBytes(byte[] bytes)
    {
        InvertirCamposDeGuid(bytes);
        return new Guid(bytes);
    }

    private static void InvertirCamposDeGuid(byte[] bytes)
    {
        if (!BitConverter.IsLittleEndian)
        {
            return;
        }

        Array.Reverse(bytes, 0, 4); // Data1 (uint)
        Array.Reverse(bytes, 4, 2); // Data2 (ushort)
        Array.Reverse(bytes, 6, 2); // Data3 (ushort)
    }
}
