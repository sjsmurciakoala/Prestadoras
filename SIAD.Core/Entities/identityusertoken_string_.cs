using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class identityusertoken_string_
{
    public string user_id { get; set; } = null!;

    public string login_provider { get; set; } = null!;

    public string name { get; set; } = null!;

    public string? value { get; set; }

    public virtual identityuser user { get; set; } = null!;
}
