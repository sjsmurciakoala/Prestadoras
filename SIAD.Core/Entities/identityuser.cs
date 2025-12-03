using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class identityuser
{
    public string id { get; set; } = null!;

    public string? user_name { get; set; }

    public string? normalized_user_name { get; set; }

    public string? email { get; set; }

    public string? normalized_email { get; set; }

    public bool email_confirmed { get; set; }

    public string? password_hash { get; set; }

    public string? security_stamp { get; set; }

    public string? concurrency_stamp { get; set; }

    public string? phone_number { get; set; }

    public bool phone_number_confirmed { get; set; }

    public bool two_factor_enabled { get; set; }

    public DateTime? lockout_end { get; set; }

    public bool lockout_enabled { get; set; }

    public int access_failed_count { get; set; }

    public virtual ICollection<identityuserclaim_string_> identityuserclaim_string_s { get; set; } = new List<identityuserclaim_string_>();

    public virtual ICollection<identityuserlogin_string_> identityuserlogin_string_s { get; set; } = new List<identityuserlogin_string_>();

    public virtual ICollection<identityusertoken_string_> identityusertoken_string_s { get; set; } = new List<identityusertoken_string_>();

    public virtual ICollection<identityrole> roles { get; set; } = new List<identityrole>();
}
