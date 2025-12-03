using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class identityuserclaim_string_
{
    public int id { get; set; }

    public string user_id { get; set; } = null!;

    public string? claim_type { get; set; }

    public string? claim_value { get; set; }

    public virtual identityuser user { get; set; } = null!;
}
