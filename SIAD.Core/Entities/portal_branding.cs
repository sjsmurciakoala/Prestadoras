namespace SIAD.Core.Entities;

public class portal_branding
{
    public int branding_id { get; set; }
    public string company_name { get; set; } = null!;
    public string company_short_name { get; set; } = string.Empty;
    public string logo_mime { get; set; } = null!;
    public byte[] logo_bytes { get; set; } = Array.Empty<byte>();

    public DateTime updated_at { get; set; }
}
