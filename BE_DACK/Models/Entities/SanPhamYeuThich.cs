using System;
using System.Collections.Generic;

namespace BE_DACK.Models.Entities;

public partial class SanPhamYeuThich
{
    public int Id { get; set; }

    public int? IdCustomer { get; set; }

    public long? IdProduct { get; set; }

    public virtual Product IdProductNavigation { get; set; }
}
