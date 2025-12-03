using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class identityrole
{
    public string id { get; set; } = null!;

    public string? name { get; set; }

    public string? normalized_name { get; set; }

    public string? concurrency_stamp { get; set; }

    public virtual ICollection<identityroleclaim_string_> identityroleclaim_string_s { get; set; } = new List<identityroleclaim_string_>();

    public virtual ICollection<identityuser> users { get; set; } = new List<identityuser>();
}
