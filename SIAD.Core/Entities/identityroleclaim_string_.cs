using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class identityroleclaim_string_
{
    public int id { get; set; }

    public string role_id { get; set; } = null!;

    public string? claim_type { get; set; }

    public string? claim_value { get; set; }

    public virtual identityrole role { get; set; } = null!;
}
