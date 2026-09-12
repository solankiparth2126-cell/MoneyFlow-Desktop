using System.Collections.Generic;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.Entities;

public class VoucherType
{
    public int VoucherTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public VoucherTypeEnum Type { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public int PaddingWidth { get; set; } = 4;
    public int StartingNumber { get; set; } = 1;
    public string RestartFrequency { get; set; } = "Yearly"; // Yearly, Monthly, Never
    public int NextNumber { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}
