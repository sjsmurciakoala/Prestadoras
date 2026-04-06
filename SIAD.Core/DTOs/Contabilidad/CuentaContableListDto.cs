namespace SIAD.Core.DTOs.Contabilidad
{
    public sealed class CuentaContableListDto
    {
        public long CuentaContableId { get; set; }
        public string? Codigo { get; set; }
        public string? Nombre { get; set; }
        public bool Activo { get; set; }
    }
}