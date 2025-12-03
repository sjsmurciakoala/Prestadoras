using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class identityuserlogin_string_
{
    public string login_provider { get; set; } = null!;

    public string provider_key { get; set; } = null!;

    public string? provider_display_name { get; set; }

    public string user_id { get; set; } = null!;

    public virtual identityuser user { get; set; } = null!;
}
