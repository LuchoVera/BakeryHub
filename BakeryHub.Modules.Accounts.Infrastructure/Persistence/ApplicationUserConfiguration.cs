using BakeryHub.Modules.Accounts.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BakeryHub.Modules.Accounts.Infrastructure.Persistence;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable(name: "AspNetUsers");
        builder.HasIndex(u => u.TenantId).IsUnique();
        builder.HasOne(u => u.AdministeredTenant)
              .WithOne()
              .HasForeignKey<ApplicationUser>(u => u.TenantId)
              .OnDelete(DeleteBehavior.SetNull);
        builder.Property(u => u.Name).HasMaxLength(150);
    }
}

public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable(name: "AspNetRoles");
    }
}

public class CustomerTenantMembershipConfiguration : IEntityTypeConfiguration<CustomerTenantMembership>
{
    public void Configure(EntityTypeBuilder<CustomerTenantMembership> builder)
    {
        builder.HasKey(ctm => new { ctm.ApplicationUserId, ctm.TenantId });

        builder.HasOne(ctm => ctm.User)
              .WithMany(u => u.TenantMemberships)
              .HasForeignKey(ctm => ctm.ApplicationUserId)
              .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ctm => ctm.Tenant)
              .WithMany()
              .HasForeignKey(ctm => ctm.TenantId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
