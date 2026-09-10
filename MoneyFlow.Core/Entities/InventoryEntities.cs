using System;

namespace MoneyFlow.Core.Entities;

public class Unit
{
    public int UnitId { get; set; }
    public int CompanyId { get; set; }
    public string UnitName { get; set; } = string.Empty; // e.g. "Nos", "Kg", "Box"
    public string FormalName { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Company? Company { get; set; }
}

public class StockItem
{
    public int StockItemId { get; set; }
    public int CompanyId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int? UnitId { get; set; }
    public decimal OpeningQuantity { get; set; }
    public decimal OpeningRate { get; set; }
    public decimal OpeningValue { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual Company? Company { get; set; }
    public virtual Unit? Unit { get; set; }
}
