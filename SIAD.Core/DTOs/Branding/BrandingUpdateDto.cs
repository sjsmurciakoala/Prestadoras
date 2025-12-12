using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Branding;

public sealed class BrandingUpdateDto
{
    [Required]
    [StringLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [StringLength(120)]
    public string CompanyShortName { get; set; } = string.Empty;
}
