using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Data.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(c => c.CompanyId);

        builder.Property(c => c.CompanyName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Address)
            .HasMaxLength(250);

        builder.Property(c => c.State)
            .HasMaxLength(100);

        builder.Property(c => c.Country)
            .HasMaxLength(100);

        builder.Property(c => c.PAN)
            .HasMaxLength(20);

        builder.Property(c => c.Email)
            .HasMaxLength(100);

        builder.Property(c => c.Phone)
            .HasMaxLength(50);

        builder.Property(c => c.Currency)
            .HasMaxLength(10);

        builder.Property(c => c.IsActive);

        builder.HasIndex(c => c.CompanyName);
    }
}

public class FinancialYearConfiguration : IEntityTypeConfiguration<FinancialYear>
{
    public void Configure(EntityTypeBuilder<FinancialYear> builder)
    {
        builder.ToTable("FinancialYears");
        builder.HasKey(f => f.FinancialYearId);

        builder.Property(f => f.YearName)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(f => f.Company)
            .WithMany(c => c.FinancialYears)
            .HasForeignKey(f => f.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => new { f.CompanyId, f.StartDate, f.EndDate });
    }
}

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups");
        builder.HasKey(g => g.GroupId);

        builder.Property(g => g.GroupName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(g => g.Nature)
            .IsRequired();

        builder.Property(g => g.IsPredefined)
            .HasDefaultValue(false);

        builder.HasOne(g => g.Company)
            .WithMany(c => c.Groups)
            .HasForeignKey(g => g.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.ParentGroup)
            .WithMany(g => g.SubGroups)
            .HasForeignKey(g => g.ParentGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(g => new { g.CompanyId, g.GroupName });
        builder.HasIndex(g => g.ParentGroupId);
        builder.HasIndex(g => new { g.CompanyId, g.IsActive });
    }
}

public class LedgerConfiguration : IEntityTypeConfiguration<Ledger>
{
    public void Configure(EntityTypeBuilder<Ledger> builder)
    {
        builder.ToTable("Ledgers");
        builder.HasKey(l => l.LedgerId);

        builder.Property(l => l.LedgerName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(l => l.OpeningBalance)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.CreditLimit)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.Address).HasMaxLength(250);
        builder.Property(l => l.Phone).HasMaxLength(50);
        builder.Property(l => l.Email).HasMaxLength(100);
        builder.Property(l => l.PAN).HasMaxLength(20);
        builder.Property(l => l.State).HasMaxLength(100);
        builder.Property(l => l.BankName).HasMaxLength(100);
        builder.Property(l => l.BankAccountNumber).HasMaxLength(50);
        builder.Property(l => l.IFSC).HasMaxLength(20);

        builder.HasOne(l => l.Company)
            .WithMany(c => c.Ledgers)
            .HasForeignKey(l => l.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Group)
            .WithMany(g => g.Ledgers)
            .HasForeignKey(l => l.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.CompanyId, l.LedgerName });
        builder.HasIndex(l => l.GroupId);
        builder.HasIndex(l => new { l.CompanyId, l.IsActive });
    }
}

public class VoucherTypeConfiguration : IEntityTypeConfiguration<VoucherType>
{
    public void Configure(EntityTypeBuilder<VoucherType> builder)
    {
        builder.ToTable("VoucherTypes");
        builder.HasKey(v => v.VoucherTypeId);

        builder.Property(v => v.Name).IsRequired().HasMaxLength(50);
        builder.Property(v => v.Code).IsRequired().HasMaxLength(10);
        builder.Property(v => v.Prefix).HasMaxLength(10);

        builder.HasIndex(v => v.Code).IsUnique();
    }
}

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("Vouchers");
        builder.HasKey(v => v.VoucherId);

        builder.Property(v => v.VoucherNumber).IsRequired().HasMaxLength(50);
        builder.Property(v => v.ReferenceNumber).HasMaxLength(50);
        builder.Property(v => v.Narration).HasMaxLength(1000);
        builder.Property(v => v.CreatedBy).HasMaxLength(50);
        builder.Property(v => v.ModifiedBy).HasMaxLength(50);
        builder.Property(v => v.IsDeleted);

        builder.HasOne(v => v.Company)
            .WithMany(c => c.Vouchers)
            .HasForeignKey(v => v.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.FinancialYear)
            .WithMany(f => f.Vouchers)
            .HasForeignKey(v => v.FinancialYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.VoucherType)
            .WithMany(vt => vt.Vouchers)
            .HasForeignKey(v => v.VoucherTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => v.CompanyId);
        builder.HasIndex(v => v.FinancialYearId);
        builder.HasIndex(v => v.VoucherDate);
        builder.HasIndex(v => v.VoucherNumber);
        builder.HasIndex(v => new { v.CompanyId, v.VoucherNumber });
        builder.HasIndex(v => new { v.CompanyId, v.FinancialYearId, v.VoucherDate, v.IsDeleted });
        builder.HasIndex(v => new { v.CompanyId, v.VoucherDate, v.IsDeleted });
        builder.HasIndex(v => new { v.CompanyId, v.VoucherTypeId, v.FinancialYearId });
    }
}

public class VoucherEntryConfiguration : IEntityTypeConfiguration<VoucherEntry>
{
    public void Configure(EntityTypeBuilder<VoucherEntry> builder)
    {
        builder.ToTable("VoucherEntries");
        builder.HasKey(e => e.VoucherEntryId);

        builder.Property(e => e.Debit)
            .HasColumnType("decimal(18,2)");

        builder.Property(e => e.Credit)
            .HasColumnType("decimal(18,2)");

        builder.Property(e => e.Narration).HasMaxLength(500);
        builder.Property(e => e.InstrumentNumber).HasMaxLength(50);

        builder.HasOne(e => e.Voucher)
            .WithMany(v => v.VoucherEntries)
            .HasForeignKey(e => e.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Ledger)
            .WithMany(l => l.VoucherEntries)
            .HasForeignKey(e => e.LedgerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.VoucherId);
        builder.HasIndex(e => e.LedgerId);
        builder.HasIndex(e => new { e.LedgerId, e.VoucherId });
    }
}

public class BillAllocationConfiguration : IEntityTypeConfiguration<BillAllocation>
{
    public void Configure(EntityTypeBuilder<BillAllocation> builder)
    {
        builder.ToTable("BillAllocations");
        builder.HasKey(b => b.BillAllocationId);

        builder.Property(b => b.BillName).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Amount).HasColumnType("decimal(18,2)");

        builder.HasOne(b => b.Company)
            .WithMany()
            .HasForeignKey(b => b.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.VoucherEntry)
            .WithMany(ve => ve.BillAllocations)
            .HasForeignKey(b => b.VoucherEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Ledger)
            .WithMany()
            .HasForeignKey(b => b.LedgerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.CompanyId, b.LedgerId, b.BillName });
        builder.HasIndex(b => b.VoucherEntryId);
    }
}
